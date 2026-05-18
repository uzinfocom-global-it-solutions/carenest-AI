using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class VoiceAction : BaseEntity
{
    public required int FamilyId { get; set; }
    public required string UserId { get; set; }
    public required VoiceActionType Type { get; set; }
    public required string Text { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public bool RequiresConfirmation { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public VoiceActionStatus Status { get; set; } = VoiceActionStatus.Pending;
    public string? Metadata { get; set; } // JSON
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool EscalationEnabled { get; set; }
    public int EscalationStep { get; set; } // 0=none, 1=resend, 2=secondary, 3=emergency
    public DateTimeOffset? LastEscalatedAt { get; set; }
    public string? EscalationTargetUserId { get; set; }

    public string? IdempotencyKey { get; set; }

    public Family Family { get; set; } = null!;
}
