using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// Push notification delivery via SSE.
/// When the app is connected, SSE delivers instantly.
/// When offline, VoiceActions are persisted and fetched on next app resume.
public sealed class LocalPushNotificationService : IPushNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ISseConnectionManager _sse;
    private readonly ILogger<LocalPushNotificationService> _logger;

    public LocalPushNotificationService(
        IApplicationDbContext db,
        ISseConnectionManager sse,
        ILogger<LocalPushNotificationService> logger)
    {
        _db = db;
        _sse = sse;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        NotificationPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        // deviceToken is not used — delivery is via SSE
        _logger.LogDebug("[LocalPush] SendAsync title={Title} priority={Priority}", title, priority);
        return true;
    }

    public async Task<int> SendToUserAsync(
        string userId,
        string title,
        string body,
        NotificationPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var payload = BuildPayload(title, body, priority, data);
        try
        {
            await _sse.PublishAsync(userId, new SseEvent("push_notification", payload), ct);
            _logger.LogDebug("[LocalPush] SSE push → user {UserId} priority={Priority}", userId, priority);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LocalPush] SSE publish failed for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> SendToFamilyAsync(
        int familyId,
        string title,
        string body,
        NotificationPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var userIds = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var sent = 0;
        var payload = BuildPayload(title, body, priority, data);

        foreach (var userId in userIds)
        {
            try
            {
                await _sse.PublishAsync(userId, new SseEvent("push_notification", payload), ct);
                sent++;
            }
            catch { /* SSE failure must not abort the loop */ }
        }

        _logger.LogDebug("[LocalPush] SSE push → family {FamilyId} sent={Sent}/{Total}", familyId, sent, userIds.Count);
        return sent;
    }

    public Task<bool> SendToTopicAsync(
        string topic,
        string title,
        string body,
        NotificationPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("[LocalPush] SendToTopic({Topic}) — no-op without FCM", topic);
        return Task.FromResult(true);
    }

    private static string BuildPayload(
        string title, string body, NotificationPriority priority, Dictionary<string, string>? data)
    {
        var obj = new Dictionary<string, object>
        {
            ["title"]     = title,
            ["body"]      = body,
            ["priority"]  = priority.ToString().ToLowerInvariant(),
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        if (data is not null)
            foreach (var (k, v) in data)
                obj[k] = v;

        return System.Text.Json.JsonSerializer.Serialize(obj);
    }
}
