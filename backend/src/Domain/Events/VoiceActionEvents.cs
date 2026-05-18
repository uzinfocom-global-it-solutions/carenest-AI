using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Events;

public class VoiceActionCreatedEvent : BaseEvent
{
    public int VoiceActionId { get; }
    public string UserId { get; }
    public int FamilyId { get; }
    public NotificationPriority Priority { get; }

    public VoiceActionCreatedEvent(int voiceActionId, string userId, int familyId, NotificationPriority priority)
    {
        VoiceActionId = voiceActionId;
        UserId = userId;
        FamilyId = familyId;
        Priority = priority;
    }
}

public class NotificationDeliveredEvent : BaseEvent
{
    public int VoiceActionId { get; }
    public string UserId { get; }
    public DateTimeOffset DeliveredAt { get; }

    public NotificationDeliveredEvent(int voiceActionId, string userId, DateTimeOffset deliveredAt)
    {
        VoiceActionId = voiceActionId;
        UserId = userId;
        DeliveredAt = deliveredAt;
    }
}

public class NotificationConfirmedEvent : BaseEvent
{
    public int VoiceActionId { get; }
    public string UserId { get; }
    public DateTimeOffset ConfirmedAt { get; }

    public NotificationConfirmedEvent(int voiceActionId, string userId, DateTimeOffset confirmedAt)
    {
        VoiceActionId = voiceActionId;
        UserId = userId;
        ConfirmedAt = confirmedAt;
    }
}

public class NotificationFailedEvent : BaseEvent
{
    public int VoiceActionId { get; }
    public string UserId { get; }
    public string Reason { get; }

    public NotificationFailedEvent(int voiceActionId, string userId, string reason)
    {
        VoiceActionId = voiceActionId;
        UserId = userId;
        Reason = reason;
    }
}
