using Backend.Application.Calendar.Models;
using Backend.Domain.Entities;

namespace Backend.Application.Calendar.Internal;

internal static class CalendarMapper
{
    public static CalendarEventResult ToResult(CalendarEvent e) => new(
        e.Id, e.FamilyId, e.ChildId, e.Title, e.Description,
        e.StartDatetime, e.EndDatetime,
        e.LocationLabel, e.LocationKey, e.Latitude, e.Longitude,
        e.LocationType, e.ActivityIntensity, e.WeatherSensitive,
        e.Source, e.CreatedByUserId, e.CreatedAt, e.ReminderMinutes);
}
