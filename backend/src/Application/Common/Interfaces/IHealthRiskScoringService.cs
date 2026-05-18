using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IHealthRiskScoringService
{
    RiskAssessment CalculateRisk(
        MonitoringIssueType issueType,
        ProactiveChildContext? child,
        ProactiveWeatherContext? weather,
        string? symptomDescription,
        int followUpCount,
        int missedFollowUps,
        MonitoringSeverity currentSeverity = MonitoringSeverity.Low);
}

public record RiskAssessment(
    int Score,
    MonitoringSeverity Severity,
    MonitoringEscalationLevel EscalationLevel,
    IReadOnlyList<string> RiskFactors,
    int NextFollowUpMinutes);
