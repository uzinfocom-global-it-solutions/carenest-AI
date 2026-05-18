namespace Backend.Application.Common.Interfaces;

public interface IChatSynchronizationService
{
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
