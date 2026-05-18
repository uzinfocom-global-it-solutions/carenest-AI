using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IVoiceActionService
{
    Task<VoiceAction?> CreateAsync(
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
        CancellationToken ct = default);

    Task ConfirmAsync(int voiceActionId, string userId, CancellationToken ct = default);
    Task SkipAsync(int voiceActionId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<VoiceAction>> GetPendingAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<VoiceAction>> GetFamilyTimelineAsync(int familyId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task MarkDeliveredAsync(int voiceActionId, CancellationToken ct = default);
}
