using Backend.Application.Common.Interfaces;
using Backend.Application.Recommendations.Internal;
using Backend.Application.Recommendations.Models;
using Backend.Domain.Entities;

namespace Backend.Application.Recommendations.Commands.GenerateRecommendations;

public record GenerateRecommendationsCommand(
    int ChildId,
    string RequestingUserId) : IRequest<IEnumerable<RecommendationResult>>;

public class GenerateRecommendationsCommandHandler
    : IRequestHandler<GenerateRecommendationsCommand, IEnumerable<RecommendationResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWeatherAdapter _weather;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public GenerateRecommendationsCommandHandler(
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

    public async Task<IEnumerable<RecommendationResult>> Handle(
        GenerateRecommendationsCommand request,
        CancellationToken cancellationToken)
    {
        var child = await _context.Children
            .Include(c => c.Sensitivity)
            .Include(c => c.Routines.Where(r => r.Active))
            .FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        var sensitivity = child.Sensitivity ?? new ChildSensitivity { ChildId = request.ChildId };

        var family = await _context.Families.FindAsync([child.FamilyId], cancellationToken);
        WeatherSnapshot? weather = null;
        if (family?.DefaultLocationKey != null)
        {
            weather = await _weather.GetLatestAsync(family.DefaultLocationKey, cancellationToken)
                   ?? await _weather.FetchAndStoreAsync(
                          family.DefaultLocationKey,
                          family.DefaultLatitude,
                          family.DefaultLongitude, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var upcomingEvents = await _context.CalendarEvents
            .Where(e => e.ChildId == request.ChildId
                     && e.StartDatetime > now
                     && e.StartDatetime < now.AddHours(24)
                     && e.WeatherSensitive)
            .ToListAsync(cancellationToken);

        var generated = new List<Recommendation>();

        if (weather != null)
            generated.AddRange(RecommendationRuleEngine.EvaluateWeatherRules(child, sensitivity, weather));

        generated.AddRange(RecommendationRuleEngine.EvaluateRoutineRules(child, sensitivity));

        foreach (var calEvent in upcomingEvents)
            generated.AddRange(RecommendationRuleEngine.EvaluateEventRules(child, sensitivity, calEvent, weather));

        if (generated.Count == 0)
            return [];

        var activeRecs = await _context.Recommendations
            .Where(r => r.ChildId == request.ChildId
                     && r.ExpiresAt > now
                     && r.Status != Backend.Domain.Enums.RecommendationStatusEnum.Dismissed
                     && r.Status != Backend.Domain.Enums.RecommendationStatusEnum.Expired)
            .ToListAsync(cancellationToken);

        var toAdd = new List<Recommendation>();
        foreach (var rec in generated)
        {
            bool isDuplicate = activeRecs.Any(ar =>
                ar.RecommendationType == rec.RecommendationType
                && ar.CalendarEventId == rec.CalendarEventId
                && ar.Title == rec.Title);

            if (!isDuplicate)
            {
                toAdd.Add(rec);
                activeRecs.Add(rec);
            }
        }

        if (toAdd.Count == 0)
            return [];

        _context.Recommendations.AddRange(toAdd);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var rec in toAdd)
        {
            await _notifications.DispatchAsync(rec.Id, cancellationToken);
            await _audit.LogAsync(request.RequestingUserId, "recommendation.generated", "recommendation", rec.Id,
                new { rec.RecommendationType, rec.Priority }, cancellationToken);
        }

        return toAdd.Select(r => new RecommendationResult(
            r.Id, r.FamilyId, r.ChildId, r.RecommendationType, r.Priority,
            r.Title, r.Message, r.Reason, r.Status, r.ExpiresAt, r.CreatedAt));
    }
}
