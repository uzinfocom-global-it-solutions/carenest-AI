using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Child : BaseEntity
{
    public required int FamilyId { get; set; }
    public required string DisplayName { get; set; }
    public required int AgeYears { get; set; }
    public AgeGroupEnum AgeGroup { get; set; }
    public string? Gender { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
    public ChildSensitivity? Sensitivity { get; set; }
    public ICollection<ChildNote> Notes { get; set; } = [];
    public ICollection<ChildRoutine> Routines { get; set; } = [];
    public ICollection<CalendarEvent> Events { get; set; } = [];
    public ICollection<Recommendation> Recommendations { get; set; } = [];

    public static AgeGroupEnum ResolveAgeGroup(int ageYears) => ageYears switch
    {
        <= 3 => AgeGroupEnum.Toddler,
        <= 9 => AgeGroupEnum.Child,
        <= 12 => AgeGroupEnum.Preteen,
        _ => AgeGroupEnum.Teen
    };
}
