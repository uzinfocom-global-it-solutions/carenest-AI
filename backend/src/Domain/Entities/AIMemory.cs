using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class AIMemory
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public AIMemoryType MemoryType { get; set; }
    public int RelevanceScore { get; set; } = 100;
    public int ObservationCount { get; set; } = 1;
    public DateTimeOffset LastObservedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}
