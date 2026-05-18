using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class VoiceActionService : IVoiceActionService
{
    private readonly IApplicationDbContext _db;
    private readonly IVoiceActionQueue _queue;
    private readonly ISseConnectionManager _sse;
    private readonly VoiceActionDeduplicationService _dedup;
    private readonly ILogger<VoiceActionService> _logger;

    public VoiceActionService(
        IApplicationDbContext db,
        IVoiceActionQueue queue,
        ISseConnectionManager sse,
        VoiceActionDeduplicationService dedup,
        ILogger<VoiceActionService> logger)
    {
        _db = db;
        _queue = queue;
        _sse = sse;
        _dedup = dedup;
        _logger = logger;
    }

    public async Task<VoiceAction?> CreateAsync(
        int familyId,
        string userId,
        VoiceActionType type,
        string text,
        NotificationPriority priority,
        bool requiresConfirmation,
        bool escalationEnabled,
        DateTimeOffset? scheduledAt,
        string? metadata,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        // Deduplication: if idempotencyKey is provided, skip if already exists
        if (idempotencyKey is not null && await _dedup.IsDuplicateAsync(idempotencyKey, ct))
            return null;

        var action = new VoiceAction
        {
            FamilyId = familyId,
            UserId = userId,
            Type = type,
            Text = text,
            Priority = priority,
            RequiresConfirmation = requiresConfirmation,
            EscalationEnabled = escalationEnabled,
            ScheduledAt = scheduledAt,
            Metadata = metadata,
            IdempotencyKey = idempotencyKey,
            Status = VoiceActionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        action.AddDomainEvent(new VoiceActionCreatedEvent(action.Id, userId, familyId, priority));

        _db.VoiceActions.Add(action);
        await _db.SaveChangesAsync(ct);

        // Scheduled reminders stay in DB; ScheduledReminderWorker will enqueue them when due.
        var isScheduled = scheduledAt.HasValue && scheduledAt.Value > DateTimeOffset.UtcNow.AddSeconds(10);
        if (!isScheduled)
            await _queue.EnqueueAsync(action, ct);

        await _sse.PublishAsync(userId, new SseEvent(
            "voice_action_created",
            JsonSerializer.Serialize(new { actionId = action.Id, type = type.ToString(), priority = priority.ToString(), text })), ct);

        _logger.LogInformation("VoiceAction {Id} created for user {UserId} type {Type}", action.Id, userId, type);
        return action;
    }

    public async Task ConfirmAsync(int voiceActionId, string userId, CancellationToken ct = default)
    {
        var action = await _db.VoiceActions
            .FirstOrDefaultAsync(a => a.Id == voiceActionId && a.UserId == userId, ct);

        if (action is null) return;

        action.ConfirmedAt = DateTimeOffset.UtcNow;
        action.Status = VoiceActionStatus.Confirmed;
        action.AddDomainEvent(new NotificationConfirmedEvent(voiceActionId, userId, DateTimeOffset.UtcNow));

        await _db.SaveChangesAsync(ct);

        await _sse.PublishAsync(userId, new SseEvent(
            "voice_action_confirmed",
            JsonSerializer.Serialize(new { actionId = voiceActionId, confirmedAt = action.ConfirmedAt })), ct);

        _logger.LogInformation("VoiceAction {Id} confirmed by user {UserId}", voiceActionId, userId);
    }

    public async Task SkipAsync(int voiceActionId, string userId, CancellationToken ct = default)
    {
        var action = await _db.VoiceActions
            .FirstOrDefaultAsync(a => a.Id == voiceActionId && a.UserId == userId, ct);

        if (action is null) return;

        action.Status = VoiceActionStatus.Skipped;
        await _db.SaveChangesAsync(ct);

        await _sse.PublishAsync(userId, new SseEvent(
            "voice_action_skipped",
            JsonSerializer.Serialize(new { actionId = voiceActionId })), ct);
    }

    public async Task<IReadOnlyList<VoiceAction>> GetPendingAsync(string userId, CancellationToken ct = default) =>
        await _db.VoiceActions
            .Where(a => a.UserId == userId &&
                        (a.Status == VoiceActionStatus.Pending || a.Status == VoiceActionStatus.Queued))
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VoiceAction>> GetFamilyTimelineAsync(
        int familyId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default) =>
        await _db.VoiceActions
            .Where(a => a.FamilyId == familyId && a.CreatedAt >= from && a.CreatedAt <= to)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

    public async Task MarkDeliveredAsync(int voiceActionId, CancellationToken ct = default)
    {
        var action = await _db.VoiceActions
            .FirstOrDefaultAsync(a => a.Id == voiceActionId, ct);

        if (action is null) return;

        action.DeliveredAt = DateTimeOffset.UtcNow;
        action.Status = VoiceActionStatus.Delivered;
        action.AddDomainEvent(new NotificationDeliveredEvent(voiceActionId, action.UserId, DateTimeOffset.UtcNow));

        await _db.SaveChangesAsync(ct);
    }
}
