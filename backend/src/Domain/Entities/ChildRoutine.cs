using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class ChildRoutine : BaseEntity
{
    public required int ChildId { get; set; }
    public required string Title { get; set; }
    public required RoutineTypeEnum RoutineType { get; set; }
    public required TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public RepeatPatternEnum RepeatPattern { get; set; } = RepeatPatternEnum.Daily;
    public LocationTypeEnum LocationType { get; set; } = LocationTypeEnum.Indoor;
    public ActivityIntensityEnum ActivityIntensity { get; set; } = ActivityIntensityEnum.Low;
    public bool WeatherSensitive { get; set; } = false;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Child Child { get; set; } = null!;
}
