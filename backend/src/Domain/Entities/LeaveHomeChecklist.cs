namespace Backend.Domain.Entities;

public class LeaveHomeChecklist
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public DateOnly Date { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string? TriggerEventTitle { get; set; }
    public DateTimeOffset? EventStartsAt { get; set; }
    public DateTimeOffset DispatchedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}
