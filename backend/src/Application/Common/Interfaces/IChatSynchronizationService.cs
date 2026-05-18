namespace Backend.Application.Common.Interfaces;

public interface IChatSynchronizationService
{
    /// <summary>
    /// Saves a proactive AI message to the family's active chat and emits SSE event.
    /// Returns the ChatMessage.Id for deep linking in push notifications.
    /// </summary>
    Task<int?> SaveProactiveMessageAsync(
        int familyId,
        string text,
        ProactiveChatMessageType messageType,
        string? correlationId = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);
}

public enum ProactiveChatMessageType
{
    MorningBriefing,
    MedicationReminder,
    WeatherAlert,
    AQIAlert,
    PollenAlert,
    LeaveHomeChecklist,
    ProactiveRecommendation,
    EscalationAlert,
    VoiceActionCreated,
    HealthAlert
}
