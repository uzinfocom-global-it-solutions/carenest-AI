using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class FamilyItem
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public int? ChildId { get; set; }
    public string Name { get; set; } = string.Empty;
    public FamilyItemCategory Category { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
    public Child? Child { get; set; }
}
