using Backend.Application.Calendar.Internal;
using Backend.Application.Calendar.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;

namespace Backend.Application.Calendar.Queries.ListCalendarEvents;

public record ListCalendarEventsQuery(
    int FamilyId,
    DateTimeOffset From,
    DateTimeOffset To,
    int? ChildId,
    string RequestingUserId) : IRequest<IReadOnlyList<CalendarEventResult>>;

public class ListCalendarEventsQueryHandler
    : IRequestHandler<ListCalendarEventsQuery, IReadOnlyList<CalendarEventResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public ListCalendarEventsQueryHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<CalendarEventResult>> Handle(
        ListCalendarEventsQuery request,
        CancellationToken cancellationToken)
    {
        await _authorization.AssertMemberAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        if (request.To <= request.From)
            throw new ValidationException([new("to", "Range end must be after range start.")]);

        var query = _context.CalendarEvents
            .Where(e => e.FamilyId == request.FamilyId
                     && e.StartDatetime >= request.From
                     && e.StartDatetime < request.To);

        if (request.ChildId.HasValue)
            query = query.Where(e => e.ChildId == request.ChildId);

        return await query
            .OrderBy(e => e.StartDatetime)
            .Select(e => new CalendarEventResult(
                e.Id, e.FamilyId, e.ChildId, e.Title, e.Description,
                e.StartDatetime, e.EndDatetime,
                e.LocationLabel, e.LocationKey, e.Latitude, e.Longitude,
                e.LocationType, e.ActivityIntensity, e.WeatherSensitive,
                e.Source, e.CreatedByUserId, e.CreatedAt, e.ReminderMinutes))
            .ToListAsync(cancellationToken);
    }
}
