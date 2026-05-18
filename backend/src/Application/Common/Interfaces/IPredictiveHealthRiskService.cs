using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

/// Predicts future risk escalation for an active monitoring session by combining
/// session age, missed follow-ups, child profile, environmental signals, and AI memory.
public interface IPredictiveHealthRiskService
{
    Task<PredictedRisk> PredictRiskAsync(
        HealthMonitoringSession session,
        ProactiveChildContext? child,
        ProactiveWeatherContext? weather,
        IReadOnlyList<AIMemory> familyMemory,
        CancellationToken ct = default);
}

public record PredictedRisk(
    double EscalationProbability,        // 0.0–1.0 probability of escalation
    MonitoringSeverity PredictedSeverity,
    int? TimeToEscalationMinutes,
    IReadOnlyList<string> RiskFactors,
    int PredictedRiskScore,              // 0-100 composite predicted score
    DateTimeOffset? PredictedEscalationAt);
