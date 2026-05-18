using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class HealthMonitoringSession
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public int? ChildId { get; set; }

    public MonitoringIssueType IssueType { get; set; }
    public MonitoringSeverity Severity { get; set; } = MonitoringSeverity.Low;
    public MonitoringStatus Status { get; set; } = MonitoringStatus.Active;
    public MonitoringEscalationLevel EscalationLevel { get; set; } = MonitoringEscalationLevel.None;

    public int RiskScore { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastInteractionAt { get; set; }
    public DateTimeOffset? NextFollowUpAt { get; set; }

    // Stores the ordered JSON array of FollowUpStep: [{minutesFromStart, question, expectedSeverity}]
    public string? MonitoringPlanJson { get; set; }

    public string? InitialSymptomDescription { get; set; }
    public string? LastUserResponse { get; set; }

    public int FollowUpCount { get; set; }
    public int MissedFollowUps { get; set; }

    public string? ResolutionSummary { get; set; }
    public bool IsResolved { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // State machine lifecycle
    public MonitoringLifecyclePhase LifecyclePhase { get; set; } = MonitoringLifecyclePhase.Detected;

    // Orchestration correlation — all voice actions, decisions, and SSE events
    // emitted for this session share this chain ID.
    public string? MonitoringChainId { get; set; }

    // Last response from user, semantically analyzed and stored as JSON.
    public string? ResponseAnalysisJson { get; set; }

    // Predictive risk fields updated by PredictiveHealthRiskService.
    public int? PredictedRiskScore { get; set; }
    public DateTimeOffset? PredictedEscalationAt { get; set; }

    // Navigation
    public Family Family { get; set; } = null!;
    public Child? Child { get; set; }
}
