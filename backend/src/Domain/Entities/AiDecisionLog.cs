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

    public string? DecisionChainId { get; set; }

    public string? IdempotencyKey { get; set; }

    public bool Executed { get; set; } = true;

    public string? SuppressedBy { get; set; }

    public string? ContextSnapshotJson { get; set; }

    public string? EscalationRationale { get; set; }

    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}
