using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IAiDecisionEngine
{
    Task<AiDecisionResult> AnalyzeAsync(
        FamilyProactiveContext context,
        IReadOnlyList<HealthMonitoringSession> activeSessions,
        CancellationToken ct = default);
}

public sealed record AiDecisionResult(
    IReadOnlyList<AiDecision> Decisions,
    IReadOnlyList<SessionRiskUpdate> SessionUpdates,
    IReadOnlyList<PredictedRisk>? PredictedRisks = null,
    string? DecisionChainId = null);

public sealed record AiDecision(
    AiDecisionType Type,
    string Message,
    NotificationPriority Priority,
    VoiceActionType ActionType,
    int? TargetChildId,
    int? RelatedSessionId,
    AiFollowUpSchedule? FollowUp,
    Dictionary<string, string>? Metadata);

public sealed record AiFollowUpSchedule(
    int InMinutes,
    string Question,
    MonitoringSeverity ExpectedSeverity);

public sealed record SessionRiskUpdate(
    int SessionId,
    MonitoringSeverity? NewSeverity,
    MonitoringEscalationLevel? NewEscalationLevel,
    int? NewRiskScore,
    DateTimeOffset? NextFollowUpAt,
    bool ShouldResolve,
    string? ResolutionSummary);

public enum AiDecisionType
{
    ProactiveMessage,
    HealthAlert,
    MedicationReminder,
    WeatherWarning,
    FollowUpCheck,
    EscalationAlert,
    HydrationReminder,
    BedtimeReminder,
    LeaveHomeAlert,
    AqiWarning,
    PollenWarning,
    PredictiveRiskAlert,   // proactive — before symptoms worsen
    MonitoringResolved,    // AI determined session can be closed
    ResponseAnalyzed       // follow-up response semantically processed
}
