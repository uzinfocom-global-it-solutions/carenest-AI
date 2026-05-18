using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class CalendarEvent : BaseEntity
{
    public required int FamilyId { get; set; }
    public int? ChildId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required DateTimeOffset StartDatetime { get; set; }
    public required DateTimeOffset EndDatetime { get; set; }
    public string? LocationLabel { get; set; }
    public string? LocationKey { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public LocationTypeEnum LocationType { get; set; } = LocationTypeEnum.Indoor;
    public ActivityIntensityEnum ActivityIntensity { get; set; } = ActivityIntensityEnum.Low;
    public bool WeatherSensitive { get; set; } = false;
    public CalendarEventSourceEnum Source { get; set; } = CalendarEventSourceEnum.Manual;
    public string? CreatedByUserId { get; set; }
    public int? ReminderMinutes { get; set; }
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}
