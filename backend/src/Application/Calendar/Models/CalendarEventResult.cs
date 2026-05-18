using Backend.Domain.Enums;

namespace Backend.Application.Calendar.Models;

public record CalendarEventResult(
    int Id,
    int FamilyId,
    int? ChildId,
    string Title,
    string? Description,
    DateTimeOffset StartDatetime,
    DateTimeOffset EndDatetime,
    string? LocationLabel,
    string? LocationKey,
    double? Latitude,
    double? Longitude,
    LocationTypeEnum LocationType,
    ActivityIntensityEnum ActivityIntensity,
    bool WeatherSensitive,
    CalendarEventSourceEnum Source,
    string? CreatedByUserId,
    DateTimeOffset CreatedAt,
    int? ReminderMinutes);

public record CalendarEventDto(
    int? ChildId,
    string Title,
    string? Description,
    DateTimeOffset StartDatetime,
    DateTimeOffset EndDatetime,
    string? LocationLabel,
    string? LocationKey,
    double? Latitude,
    double? Longitude,
    string LocationType,
    string ActivityIntensity,
    bool WeatherSensitive,
    int? ReminderMinutes = 15);
