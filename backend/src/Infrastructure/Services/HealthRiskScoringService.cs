using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Services;

public sealed class HealthRiskScoringService : IHealthRiskScoringService
{
    private static readonly Dictionary<MonitoringIssueType, int> BaseScores = new()
    {
        [MonitoringIssueType.Fever]                   = 40,
        [MonitoringIssueType.Asthma]                  = 55,
        [MonitoringIssueType.Vomiting]                = 45,
        [MonitoringIssueType.RespiratoryMonitoring]   = 50,
        [MonitoringIssueType.Cough]                   = 30,
        [MonitoringIssueType.Allergy]                 = 35,
        [MonitoringIssueType.Rash]                    = 25,
        [MonitoringIssueType.MedicationTracking]      = 35,
        [MonitoringIssueType.Hydration]               = 20,
        [MonitoringIssueType.SleepMonitoring]         = 15,
        [MonitoringIssueType.StressMonitoring]        = 15,
        [MonitoringIssueType.Unknown]                 = 20,
    };

    public RiskAssessment CalculateRisk(
        MonitoringIssueType issueType,
        ProactiveChildContext? child,
        ProactiveWeatherContext? weather,
        string? symptomDescription,
        int followUpCount,
        int missedFollowUps,
        MonitoringSeverity currentSeverity = MonitoringSeverity.Low)
    {
        var score = BaseScores.TryGetValue(issueType, out var b) ? b : 20;
        var factors = new List<string>();

        // Child sensitivity modifiers
        if (child is not null)
        {
            if (child.Sensitivities.Contains("дыхательные пути / астма") || child.Sensitivities.Contains("respiratory / asthma"))
            {
                if (issueType is MonitoringIssueType.Cough or MonitoringIssueType.Asthma or MonitoringIssueType.RespiratoryMonitoring)
                {
                    score += 20;
                    factors.Add("respiratory sensitivity");
                }
                if (issueType == MonitoringIssueType.Fever) { score += 10; factors.Add("asthma + fever"); }
            }
            if (child.Sensitivities.Contains("качество воздуха / загрязнение") || child.Sensitivities.Contains("air quality / pollution"))
            {
                if (weather?.AqiIndex > 100) { score += 15; factors.Add("AQI + air quality sensitivity"); }
            }
            if (child.Sensitivities.Contains("холод") || child.Sensitivities.Contains("cold"))
            {
                if (weather?.TemperatureCelsius < 5) { score += 10; factors.Add("cold sensitivity + low temperature"); }
            }
            if (child.Sensitivities.Contains("пыльца / аллергия") || child.Sensitivities.Contains("pollen / hay fever"))
            {
                if (issueType is MonitoringIssueType.Allergy or MonitoringIssueType.Cough) { score += 10; factors.Add("pollen sensitivity + allergy/cough"); }
            }
        }

        // Symptom-specific text analysis
        if (!string.IsNullOrEmpty(symptomDescription))
        {
            var desc = symptomDescription.ToLowerInvariant();
            if (desc.Contains("39") || desc.Contains("39.") || desc.Contains("высокая температура")) { score += 20; factors.Add("high fever ≥ 39°C"); }
            if (desc.Contains("39.5") || desc.Contains("40")) { score += 30; factors.Add("critical fever ≥ 39.5°C"); }
            if (desc.Contains("не дышит") || desc.Contains("не может дышать") || desc.Contains("cannot breathe")) { score += 35; factors.Add("breathing difficulty"); }
            if (desc.Contains("рвот") || desc.Contains("vomit")) { score += 15; factors.Add("vomiting"); }
            if (desc.Contains("потеря сознания") || desc.Contains("unconscious")) { score += 50; factors.Add("loss of consciousness"); }
        }

        // Weather modifiers
        if (weather is not null)
        {
            if (weather.AqiIndex > 150 && issueType is MonitoringIssueType.Asthma or MonitoringIssueType.RespiratoryMonitoring)
            { score += 20; factors.Add("dangerous AQI + respiratory issue"); }
            if (weather.TemperatureCelsius > 35 && issueType == MonitoringIssueType.Fever)
            { score += 10; factors.Add("extreme heat + fever"); }
        }

        // Follow-up behavior
        score += missedFollowUps * 8;
        if (missedFollowUps > 0) factors.Add($"{missedFollowUps} missed follow-up(s)");

        // Escalation for persistent issues
        if (followUpCount >= 3 && currentSeverity >= MonitoringSeverity.High)
        { score += 15; factors.Add("persistent high-severity issue"); }

        score = Math.Clamp(score, 0, 100);

        var severity = score switch
        {
            <= 30  => MonitoringSeverity.Low,
            <= 50  => MonitoringSeverity.Medium,
            <= 70  => MonitoringSeverity.High,
            <= 85  => MonitoringSeverity.Critical,
            _      => MonitoringSeverity.Emergency,
        };

        var escalation = severity switch
        {
            MonitoringSeverity.Low      => MonitoringEscalationLevel.None,
            MonitoringSeverity.Medium   => MonitoringEscalationLevel.Medium,
            MonitoringSeverity.High     => MonitoringEscalationLevel.High,
            MonitoringSeverity.Critical => MonitoringEscalationLevel.Critical,
            MonitoringSeverity.Emergency => MonitoringEscalationLevel.Emergency,
            _ => MonitoringEscalationLevel.None,
        };

        // Dynamic follow-up intervals based on severity
        var nextFollowUpMinutes = severity switch
        {
            MonitoringSeverity.Low      => 60,
            MonitoringSeverity.Medium   => 30,
            MonitoringSeverity.High     => 20,
            MonitoringSeverity.Critical => 10,
            MonitoringSeverity.Emergency => 5,
            _ => 60,
        };

        return new RiskAssessment(score, severity, escalation, factors.AsReadOnly(), nextFollowUpMinutes);
    }
}
