using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IHealthMonitoringService
{
    Task<HealthMonitoringSession> StartOrUpdateSessionAsync(
        int familyId,
        int? childId,
        MonitoringIssueType issueType,
        string symptomDescription,
        RiskAssessment risk,
        CancellationToken ct = default);

    Task UpdateSessionRiskAsync(
        int sessionId,
        RiskAssessment risk,
        string? userResponse,
        CancellationToken ct = default);

    Task ResolveSessionAsync(
        int sessionId,
        string summary,
        CancellationToken ct = default);

    Task MarkFollowUpSentAsync(
        int sessionId,
        DateTimeOffset nextFollowUpAt,
        CancellationToken ct = default);

    Task IncrementMissedFollowUpAsync(
        int sessionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<HealthMonitoringSession>> GetActiveSessionsForFamilyAsync(
        int familyId,
        CancellationToken ct = default);

    Task<HealthMonitoringSession?> FindExistingSessionAsync(
        int familyId,
        int? childId,
        MonitoringIssueType issueType,
        CancellationToken ct = default);

    Task UpdateLifecyclePhaseAsync(
        int sessionId,
        MonitoringLifecyclePhase phase,
        CancellationToken ct = default);

    Task UpdatePredictedRiskAsync(
        int sessionId,
        int predictedRiskScore,
        DateTimeOffset? predictedEscalationAt,
        CancellationToken ct = default);

    Task UpdateResponseAnalysisAsync(
        int sessionId,
        string userResponse,
        string analysisJson,
        CancellationToken ct = default);

    Task<IReadOnlyList<HealthMonitoringSession>> GetSessionsAwaitingFollowUpAsync(
        CancellationToken ct = default);
}
