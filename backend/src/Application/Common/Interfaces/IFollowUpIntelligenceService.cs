using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

/// Analyzes user responses to monitoring follow-ups, adapts timing and severity,
/// and manages the chained follow-up state machine for each session.
public interface IFollowUpIntelligenceService
{
    /// Parse a user's free-text response to a follow-up question and extract
    /// symptom trajectory, temperature, and criticality signals.
    Task<ResponseAnalysisResult> AnalyzeResponseAsync(
        int sessionId,
        string userResponse,
        CancellationToken ct = default);

    /// Build the next adaptive follow-up question for the session based on
    /// current severity, follow-up count, and the monitoring plan.
    Task<AdaptiveFollowUp?> GenerateNextFollowUpAsync(
        HealthMonitoringSession session,
        CancellationToken ct = default);

    /// Return the ordered follow-up chain for observability and debug display.
    Task<IReadOnlyList<FollowUpChainEntry>> GetFollowUpChainAsync(
        int sessionId,
        CancellationToken ct = default);

    /// Find active sessions that have a recently-elapsed follow-up window
    /// so SendMessage can route user responses to the correct session.
    Task<HealthMonitoringSession?> FindSessionAwaitingResponseAsync(
        int familyId,
        CancellationToken ct = default);
}

public record ResponseAnalysisResult(
    int SessionId,
    SymptomChangeType SymptomChange,
    double? ExtractedTemperature,
    int ScoreModifier,
    bool ShouldEscalate,
    bool ShouldResolve,
    int NextIntervalMinutes,
    string AnalysisSummary,
    string? ResponseAnalysisJson);

public enum SymptomChangeType { Unknown, Improved, Worsened, Stable }

public record AdaptiveFollowUp(
    string Question,
    int IntervalMinutes,
    MonitoringSeverity ExpectedSeverity,
    bool RequiresEscalation);

public record FollowUpChainEntry(
    int StepIndex,
    string Question,
    DateTimeOffset? SentAt,
    string? UserResponse,
    SymptomChangeType? ResponseType);
