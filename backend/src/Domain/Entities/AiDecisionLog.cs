namespace Backend.Domain.Entities;

public class AiDecisionLog
{
    public int Id { get; set; }
    public int FamilyId { get; set; }

    public string DecisionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";

    public int? TargetChildId { get; set; }
    public int? RelatedSessionId { get; set; }

    /// Chains all decisions, voice actions, and SSE events from one orchestration cycle.
    public string? DecisionChainId { get; set; }

    /// Idempotency key used for deduplication.
    public string? IdempotencyKey { get; set; }

    public bool Executed { get; set; } = true;

    /// Non-null when this decision was suppressed by the priority engine.
    public string? SuppressedBy { get; set; }

    /// Snapshot of the context at decision time (weather, AQI, child state).
    public string? ContextSnapshotJson { get; set; }

    /// Reasoning chain that produced the decision (risk factors, escalation logic).
    public string? EscalationRationale { get; set; }

    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}
