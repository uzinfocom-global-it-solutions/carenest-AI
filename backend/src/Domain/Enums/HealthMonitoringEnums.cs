namespace Backend.Domain.Enums;

public enum MonitoringIssueType
{
    Fever,
    Cough,
    Asthma,
    Allergy,
    MedicationTracking,
    Hydration,
    SleepMonitoring,
    StressMonitoring,
    RespiratoryMonitoring,
    Vomiting,
    Rash,
    Unknown
}

public enum MonitoringSeverity
{
    Low      = 1,
    Medium   = 2,
    High     = 3,
    Critical = 4,
    Emergency = 5
}

public enum MonitoringStatus
{
    Active,
    Monitoring,
    Escalated,
    Resolved,
    Abandoned
}

public enum MonitoringEscalationLevel
{
    None      = 0,
    Medium    = 1,
    High      = 2,
    Critical  = 3,
    Emergency = 4
}

/// Controls the state machine for a HealthMonitoringSession's lifecycle.
public enum MonitoringLifecyclePhase
{
    Detected,    // Symptom just logged — initial risk assessment
    Monitoring,  // Active follow-up cycle running
    Escalated,   // Risk increasing — shorter intervals
    Critical,    // Requires immediate family attention
    Stabilizing, // Symptoms improving — longer intervals
    Resolved,    // Session complete, outcome recorded
    Archived     // Historical — no longer active
}

/// State machine for a single step in a follow-up chain.
public enum FollowUpChainState
{
    Pending,      // Scheduled, not yet dispatched
    Delivered,    // Question sent to family chat
    Acknowledged, // User replied to this follow-up
    Delayed,      // Next check postponed (user requested delay)
    Escalated,    // Escalated to emergency due to no response
    Expired,      // Window elapsed without response — session auto-advanced
    Completed     // Step done with resolution confirmed
}
