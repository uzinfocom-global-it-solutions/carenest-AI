using Backend.Application.Common.Interfaces;
using Backend.Application.Recommendations.Models;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Recommendations.Commands.GenerateEventRecommendation;

public record GenerateEventRecommendationCommand(
    int CalendarEventId,
    string RequestingUserId) : IRequest<RecommendationResult?>;

public class GenerateEventRecommendationCommandHandler
    : IRequestHandler<GenerateEventRecommendationCommand, RecommendationResult?>
{
    private readonly IApplicationDbContext _context;
    private readonly IWeatherAdapter _weather;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public GenerateEventRecommendationCommandHandler(
        IApplicationDbContext context,
        IWeatherAdapter weather,
        INotificationService notifications,
        IAuditService audit)
    {
        _context = context;
        _weather = weather;
        _notifications = notifications;
        _audit = audit;
    }

    public async Task<RecommendationResult?> Handle(
        GenerateEventRecommendationCommand request, CancellationToken ct)
    {
        var ev = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == request.CalendarEventId, ct);
        if (ev is null) return null;

        var alreadyExists = await _context.Recommendations
            .AnyAsync(r => r.CalendarEventId == ev.Id, ct);
        if (alreadyExists) return null;

        var childId = ev.ChildId
            ?? await _context.Children
                .Where(c => c.FamilyId == ev.FamilyId)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
        if (childId is null) return null;

        var child = await _context.Children
            .Include(c => c.Sensitivity)
            .FirstAsync(c => c.Id == childId, ct);

        var family = await _context.Families.FindAsync([ev.FamilyId], ct);
        WeatherSnapshot? weather = null;
        if (family?.DefaultLocationKey is not null)
        {
            weather = await _weather.GetLatestAsync(family.DefaultLocationKey, ct);
        }

        var (title, message, reason, priority) = BuildAdvice(child, ev, weather);

        var rec = new Recommendation
        {
            FamilyId = ev.FamilyId,
            ChildId = child.Id,
            CalendarEventId = ev.Id,
            WeatherSnapshotId = weather?.Id,
            RecommendationType = ChooseType(ev),
            Priority = priority,
            Title = title,
            Message = message,
            Reason = reason,
            DeliveryType = DeliveryTypeEnum.InApp,
            Status = RecommendationStatusEnum.Pending,
            GeneratedBy = "worker:event-window",
            ExpiresAt = ev.EndDatetime.AddHours(2),
        };

        _context.Recommendations.Add(rec);
        await _context.SaveChangesAsync(ct);

        await _notifications.DispatchAsync(rec.Id, ct);
        await _audit.LogAsync(request.RequestingUserId, "recommendation.event_generated",
            "recommendation", rec.Id,
            new { eventId = ev.Id, ev.Title, rec.Priority }, ct);

        return new RecommendationResult(
            rec.Id, rec.FamilyId, rec.ChildId, rec.RecommendationType, rec.Priority,
            rec.Title, rec.Message, rec.Reason, rec.Status, rec.ExpiresAt, rec.CreatedAt);
    }

    private static RecommendationTypeEnum ChooseType(CalendarEvent ev)
    {
        if (ev.WeatherSensitive) return RecommendationTypeEnum.WeatherAlert;
        if (ev.LocationType == LocationTypeEnum.Outdoor) return RecommendationTypeEnum.OutdoorActivity;
        return RecommendationTypeEnum.Reminder;
    }

    private static (string title, string message, string reason, string priority)
        BuildAdvice(Child child, CalendarEvent ev, WeatherSnapshot? weather)
    {
        var minutesUntil = (int)Math.Max(0, (ev.StartDatetime - DateTimeOffset.UtcNow).TotalMinutes);
        var when = minutesUntil < 5
            ? "now"
            : minutesUntil < 60
                ? $"in {minutesUntil} min"
                : $"at {ev.StartDatetime.ToLocalTime():HH:mm}";

        var title = $"Heads-up: {ev.Title} {when}";
        var location = string.IsNullOrWhiteSpace(ev.LocationLabel) ? "" : $" at {ev.LocationLabel}";

        var weatherNote = weather is null
            ? ""
            : $" Weather: {weather.WeatherCondition.ToString().ToLower()}, {Math.Round(weather.TemperatureC)}°C.";

        string message;
        string reason;
        string priority;

        if (ev.LocationType == LocationTypeEnum.Outdoor && child.Sensitivity?.AirQualitySensitive == true)
        {
            message = $"{child.DisplayName} has an outdoor event{location}. Check air quality before heading out.{weatherNote}";
            reason = "Outdoor event + air-quality-sensitive child";
            priority = "High";
        }
        else if (ev.WeatherSensitive)
        {
            message = $"{child.DisplayName}'s {ev.Title.ToLower()} is weather-sensitive{location}.{weatherNote}";
            reason = "Event flagged as weather-sensitive";
            priority = "Medium";
        }
        else
        {
            message = $"{child.DisplayName}: {ev.Title} starts {when}{location}.";
            reason = "Upcoming event in the next hour";
            priority = "Low";
        }

        return (title, message, reason, priority);
    }
}
