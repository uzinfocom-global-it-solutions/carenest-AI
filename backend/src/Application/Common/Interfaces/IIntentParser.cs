namespace Backend.Application.Common.Interfaces;

public interface IIntentParser
{
    Task<ParsedIntent> ParseAsync(string message, string userId, IntentContext? context = null, string locale = "en", CancellationToken ct = default);
}

public record IntentContext(
    int FamilyId,
    string FamilyName,
    IReadOnlyList<KnownChild> KnownChildren,
    IReadOnlyList<ChildContext> ChildrenDetail,
    IReadOnlyList<ConversationMessage> RecentHistory,
    WeatherContext? Weather = null,
    IReadOnlyList<UpcomingEventContext>? UpcomingEvents = null,
    IReadOnlyList<string>? RecentRecommendations = null,
    string? ConversationSummary = null);

public record WeatherContext(
    string LocationKey,
    string Condition,
    double TemperatureC,
    double? FeelsLikeC,
    int? HumidityPercent,
    double? WindKmh,
    double? UvIndex,
    string? Aqi,
    string? PollenLevel,
    DateTimeOffset CollectedAt);

public record UpcomingEventContext(
    string Title,
    string ChildName,
    DateTimeOffset StartDatetime,
    string LocationType,
    bool WeatherSensitive);

public record KnownChild(int Id, string DisplayName, int AgeYears);

public record ChildContext(
    int Id,
    string DisplayName,
    int AgeYears,
    string AgeGroup,
    ChildSensitivityContext? Sensitivity,
    IReadOnlyList<string> RecentNotes,
    IReadOnlyList<string> ActiveRoutines);

public record ChildSensitivityContext(
    bool HeatSensitive,
    bool ColdSensitive,
    bool AirQualitySensitive,
    bool PollenSensitive,
    bool UvSensitive,
    bool SkinSensitive,
    bool ActivitySensitive,
    bool RespiratorySensitive);

public record ConversationMessage(string Role, string Content);

public record ParsedIntent(
    string Intent,
    string? ChildName,
    string? Topic,
    bool TriggersRecommendation,
    string? RawJson,
    AssistantProposal? Proposal = null,
    string? Reply = null,
    FollowUpInfo? FollowUp = null,
    string? DetectedIssueType = null);

/// <summary>
/// Instructs the system to send an automatic follow-up message after a delay.
/// Used when the AI detects a health situation (e.g. fever) and wants to check back.
/// </summary>
public record FollowUpInfo(int InMinutes, string Question);

/// <summary>
/// A structured action the assistant has detected in the user's message and
/// would like the user to confirm before executing (e.g. "add Mila, 5").
/// </summary>
public record AssistantProposal(
    string Type,           // add_child, add_event, add_routine, add_note, update_sensitivity
    string Summary,        // human-readable description for the chat bubble
    string ParametersJson  // canonical JSON payload the confirm endpoint will execute
);
