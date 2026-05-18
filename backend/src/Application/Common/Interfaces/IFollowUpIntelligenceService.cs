using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IFollowUpIntelligenceService
{
    Task<ResponseAnalysisResult> AnalyzeResponseAsync(
        int sessionId,
        string userResponse,
        CancellationToken ct = default);

    Task<AdaptiveFollowUp?> GenerateNextFollowUpAsync(
        HealthMonitoringSession session,
        CancellationToken ct = default);

    Task<IReadOnlyList<FollowUpChainEntry>> GetFollowUpChainAsync(
        int sessionId,
        CancellationToken ct = default);

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
