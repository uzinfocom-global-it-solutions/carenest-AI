using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// Predicts future escalation probability for an active monitoring session.
/// Combines session age, missed follow-ups, child age, environmental signals,
/// chronic conditions, and AI memory of recurring symptoms.
public sealed class PredictiveHealthRiskService : IPredictiveHealthRiskService
{
    private readonly ILogger<PredictiveHealthRiskService> _logger;

    public PredictiveHealthRiskService(ILogger<PredictiveHealthRiskService> logger)
    {
        _logger = logger;
    }

    public Task<PredictedRisk> PredictRiskAsync(
        HealthMonitoringSession session,
        ProactiveChildContext? child,
        ProactiveWeatherContext? weather,
        IReadOnlyList<AIMemory> familyMemory,
        CancellationToken ct = default)
    {
        var factors = new List<string>();
        double prob = BaseEscalationProbability(session.Severity);

        // Session age — longer active = higher unresolved risk
        var durationHours = (DateTimeOffset.UtcNow - session.StartedAt).TotalHours;
        if (durationHours > 12)     { prob += 0.15; factors.Add("issue >12h unresolved"); }
        else if (durationHours > 8) { prob += 0.10; factors.Add("issue >8h unresolved"); }
        else if (durationHours > 4) { prob += 0.05; factors.Add("issue >4h unresolved"); }

        // Missed follow-ups — each one signals non-engagement
        prob += session.MissedFollowUps * 0.06;
        if (session.MissedFollowUps >= 2)
            factors.Add($"{session.MissedFollowUps} missed check-in(s)");

        // Lifecycle phase context
        if (session.LifecyclePhase is MonitoringLifecyclePhase.Escalated or MonitoringLifecyclePhase.Critical)
        { prob += 0.15; factors.Add($"lifecycle={session.LifecyclePhase}"); }

        // Child age — younger children escalate faster
        if (child is not null)
        {
            if (child.AgeYears <= 1)      { prob += 0.25; factors.Add("infant (<1y)"); }
            else if (child.AgeYears <= 3) { prob += 0.15; factors.Add("toddler (<3y)"); }
            else if (child.AgeYears <= 6) { prob += 0.07; factors.Add("young child (<6y)"); }

            // Respiratory sensitivity compound
            bool hasRespiratory = child.Sensitivities.Any(s =>
                s.Contains("астм", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("asthma", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("дыхательн", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("respiratory", StringComparison.OrdinalIgnoreCase));

            if (hasRespiratory && weather?.AqiIndex > 100)
            {
                prob += 0.20;
                factors.Add($"respiratory sensitivity + AQI {weather.AqiIndex}");
            }

            // Heat sensitivity in extreme heat
            if (child.Sensitivities.Any(s =>
                    s.Contains("жар", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("heat", StringComparison.OrdinalIgnoreCase)) &&
                weather?.TemperatureCelsius > 35)
            {
                prob += 0.12;
                factors.Add($"heat sensitivity + {weather.TemperatureCelsius:0}°C");
            }

            // Cold sensitivity in freezing weather
            if (child.Sensitivities.Any(s =>
                    s.Contains("холод", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("cold", StringComparison.OrdinalIgnoreCase)) &&
                weather?.TemperatureCelsius < 0)
            {
                prob += 0.10;
                factors.Add($"cold sensitivity + {weather.TemperatureCelsius:0}°C");
            }
        }

        // Environmental compound risks for respiratory issues
        if (weather is not null)
        {
            if (weather.AqiIndex > 150 &&
                session.IssueType is MonitoringIssueType.Asthma or
                                     MonitoringIssueType.RespiratoryMonitoring or
                                     MonitoringIssueType.Cough)
            {
                prob += 0.20;
                factors.Add($"dangerous AQI ({weather.AqiIndex}) + {session.IssueType}");
            }

            if (weather.TemperatureCelsius > 38 && session.IssueType == MonitoringIssueType.Fever)
            {
                prob += 0.12;
                factors.Add($"extreme outdoor heat ({weather.TemperatureCelsius:0}°C) + fever");
            }
        }

        // AI memory — prior escalation history for this issue type
        bool priorEscalation = familyMemory.Any(m =>
            m.Key.Contains(session.IssueType.ToString(), StringComparison.OrdinalIgnoreCase) &&
            (m.MemoryType == AIMemoryType.SymptomHistory || m.MemoryType == AIMemoryType.BehaviorPattern));

        if (priorEscalation) { prob += 0.10; factors.Add("recurring symptom pattern in memory"); }

        // Persistent high-severity with many follow-ups
        if (session.FollowUpCount >= 4 && session.Severity >= MonitoringSeverity.High)
        { prob += 0.08; factors.Add("persistent high-severity (≥4 follow-ups)"); }

        prob = Math.Clamp(prob, 0.0, 1.0);

        var predictedSeverity = prob switch
        {
            > 0.85 => MonitoringSeverity.Emergency,
            > 0.70 => MonitoringSeverity.Critical,
            > 0.50 => MonitoringSeverity.High,
            > 0.30 => MonitoringSeverity.Medium,
            _      => MonitoringSeverity.Low,
        };

        int? timeToEscalationMinutes = prob switch
        {
            > 0.80 => 30,
            > 0.65 => 90,
            > 0.45 => 180,
            > 0.25 => 360,
            _      => null,
        };

        var predictedAt = timeToEscalationMinutes.HasValue
            ? DateTimeOffset.UtcNow.AddMinutes(timeToEscalationMinutes.Value)
            : (DateTimeOffset?)null;

        var predictedScore = (int)Math.Round(prob * 100);

        _logger.LogDebug(
            "Predicted risk for session {SessionId} ({IssueType}): prob={Prob:P0}, severity={Severity}, escalationIn={Min}min",
            session.Id, session.IssueType, prob, predictedSeverity, timeToEscalationMinutes);

        return Task.FromResult(new PredictedRisk(
            prob, predictedSeverity, timeToEscalationMinutes,
            factors.AsReadOnly(), predictedScore, predictedAt));
    }

    private static double BaseEscalationProbability(MonitoringSeverity current) => current switch
    {
        MonitoringSeverity.Emergency => 0.70,
        MonitoringSeverity.Critical  => 0.55,
        MonitoringSeverity.High      => 0.35,
        MonitoringSeverity.Medium    => 0.15,
        _                            => 0.05,
    };
}
