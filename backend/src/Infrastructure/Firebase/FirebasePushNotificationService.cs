using Backend.Application.Common.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainPriority = Backend.Domain.Enums.NotificationPriority;
using FcmNotifPriority = FirebaseAdmin.Messaging.NotificationPriority;

namespace Backend.Infrastructure.Firebase;

public sealed class FirebasePushNotificationService : IPushNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private readonly FirebaseMessaging _messaging;

    public FirebasePushNotificationService(
        IApplicationDbContext db,
        ILogger<FirebasePushNotificationService> logger,
        FirebaseMessaging messaging)
    {
        _db = db;
        _logger = logger;
        _messaging = messaging;
    }

    public async Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        DomainPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var message = BuildMessage(deviceToken, title, body, priority, data);
        return await TrySendAsync(message, deviceToken, ct);
    }

    public async Task<int> SendToUserAsync(
        string userId,
        string title,
        string body,
        DomainPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await _db.DeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => t.Token)
            .ToListAsync(ct);

        return await SendToTokensAsync(tokens, title, body, priority, data, ct);
    }

    public async Task<int> SendToFamilyAsync(
        int familyId,
        string title,
        string body,
        DomainPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var userIds = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == Backend.Domain.Enums.FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var tokens = await _db.DeviceTokens
            .Where(t => userIds.Contains(t.UserId) && t.IsActive)
            .Select(t => t.Token)
            .ToListAsync(ct);

        return await SendToTokensAsync(tokens, title, body, priority, data, ct);
    }

    public async Task<bool> SendToTopicAsync(
        string topic,
        string title,
        string body,
        DomainPriority priority,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var message = new Message
        {
            Topic = topic,
            Notification = new Notification { Title = title, Body = body },
            Data = BuildDataPayload(priority, data),
            Android = BuildAndroidConfig(priority),
            Apns = BuildApnsConfig(priority),
        };

        try
        {
            var messageId = await _messaging.SendAsync(message, ct);
            _logger.LogInformation("FCM topic send to {Topic}: {MessageId}", topic, messageId);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "FCM topic send failed for topic {Topic}", topic);
            return false;
        }
    }

    private async Task<int> SendToTokensAsync(
        List<string> tokens,
        string title,
        string body,
        DomainPriority priority,
        Dictionary<string, string>? data,
        CancellationToken ct)
    {
        if (tokens.Count == 0) return 0;

        var successCount = 0;
        foreach (var batch in tokens.Chunk(500))
        {
            var multicast = new MulticastMessage
            {
                Tokens = batch.ToList(),
                Notification = new Notification { Title = title, Body = body },
                Data = BuildDataPayload(priority, data),
                Android = BuildAndroidConfig(priority),
                Apns = BuildApnsConfig(priority),
            };

            try
            {
                var response = await _messaging.SendEachForMulticastAsync(multicast, ct);
                successCount += response.SuccessCount;
                await HandleFailedTokensAsync(batch.ToList(), response, ct);
                _logger.LogInformation("FCM multicast: {Success}/{Total} delivered", response.SuccessCount, batch.Length);
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex, "FCM multicast batch failed");
            }
        }

        return successCount;
    }

    private async Task HandleFailedTokensAsync(List<string> tokens, BatchResponse response, CancellationToken ct)
    {
        var staleTokens = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var r = response.Responses[i];
            if (!r.IsSuccess && r.Exception?.MessagingErrorCode is
                MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                staleTokens.Add(tokens[i]);
        }

        if (staleTokens.Count > 0)
        {
            var stale = await _db.DeviceTokens.Where(t => staleTokens.Contains(t.Token)).ToListAsync(ct);
            foreach (var t in stale) t.IsActive = false;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Deactivated {Count} stale FCM tokens", stale.Count);
        }
    }

    private async Task<bool> TrySendAsync(Message message, string token, CancellationToken ct)
    {
        var attempt = 0;
        const int maxAttempts = 3;

        while (attempt < maxAttempts)
        {
            try
            {
                var messageId = await _messaging.SendAsync(message, ct);
                _logger.LogDebug("FCM sent to {Token}: {MessageId}", token[..10], messageId);
                return true;
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
            {
                await DeactivateTokenAsync(token, ct);
                return false;
            }
            catch (FirebaseMessagingException ex) when (attempt < maxAttempts - 1)
            {
                attempt++;
                _logger.LogWarning(ex, "FCM send attempt {Attempt} failed, retrying", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex, "FCM send failed after {Attempts} attempts", maxAttempts);
                return false;
            }
        }

        return false;
    }

    private async Task DeactivateTokenAsync(string token, CancellationToken ct)
    {
        var entity = await _db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
        if (entity is not null)
        {
            entity.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static Message BuildMessage(string token, string title, string body, DomainPriority priority, Dictionary<string, string>? data) =>
        new()
        {
            Token = token,
            Notification = new Notification { Title = title, Body = body },
            Data = BuildDataPayload(priority, data),
            Android = BuildAndroidConfig(priority),
            Apns = BuildApnsConfig(priority),
        };

    private static Dictionary<string, string> BuildDataPayload(DomainPriority priority, Dictionary<string, string>? extra)
    {
        var payload = new Dictionary<string, string>
        {
            ["priority"] = priority.ToString().ToLowerInvariant(),
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        };
        if (extra is not null)
            foreach (var (k, v) in extra) payload[k] = v;
        return payload;
    }

    private static AndroidConfig BuildAndroidConfig(DomainPriority priority) => new()
    {
        Priority = priority >= DomainPriority.High ? Priority.High : Priority.Normal,
        Notification = new AndroidNotification
        {
            ChannelId = priority == DomainPriority.Emergency
                ? "emergency_channel"
                : priority == DomainPriority.High
                    ? "high_priority_channel"
                    : "default_channel",
            Priority = priority >= DomainPriority.High ? FcmNotifPriority.MAX : FcmNotifPriority.DEFAULT,
            DefaultVibrateTimings = true,
            DefaultSound = true,
        },
    };

    private static ApnsConfig BuildApnsConfig(DomainPriority priority) => new()
    {
        Headers = new Dictionary<string, string>
        {
            ["apns-priority"] = priority >= DomainPriority.High ? "10" : "5",
            ["apns-push-type"] = "alert",
        },
        Aps = new Aps
        {
            // CriticalSound and Sound are mutually exclusive in APNs.
            Sound = priority == DomainPriority.Emergency ? null : "default",
            CriticalSound = priority == DomainPriority.Emergency
                ? new CriticalSound { Name = "default", Volume = 1.0 }
                : null,
            Badge = 1,
        },
    };
}
