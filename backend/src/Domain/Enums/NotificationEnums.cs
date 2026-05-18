namespace Backend.Domain.Enums;

public enum InputModeEnum { Voice, Text }

public enum OutputModeEnum { Voice, Text, Both }

public enum NotificationLevelEnum { Low, Normal, High }

public enum ProactiveModeEnum { Minimal, Balanced, Active }

public enum DeliveryTypeEnum { Voice, Push, InApp }

public enum DeliveryChannelEnum { Push, Voice, InApp }

public enum DeliveryStatusEnum { Pending, Delivered, Opened, Failed }

public enum NotificationPriority { Low, Normal, High, Emergency }

public enum VoiceActionType
{
    MedicationReminder,
    ProactiveRecommendation,
    CalendarAlert,
    EmergencyAlert,
    RoutineReminder,
    Custom,
    FollowUpCheck
}

public enum VoiceActionStatus
{
    Pending,
    Queued,
    Delivered,
    Confirmed,
    Skipped,
    Failed,
    Escalated
}
