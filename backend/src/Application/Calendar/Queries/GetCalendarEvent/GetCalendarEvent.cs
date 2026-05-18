using Backend.Application.Calendar.Internal;
using Backend.Application.Calendar.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Calendar.Queries.GetCalendarEvent;

public record GetCalendarEventQuery(int EventId, string RequestingUserId) : IRequest<CalendarEventResult>;

public class GetCalendarEventQueryHandler : IRequestHandler<GetCalendarEventQuery, CalendarEventResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public GetCalendarEventQueryHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<CalendarEventResult> Handle(GetCalendarEventQuery request, CancellationToken cancellationToken)
    {
        var ev = await _context.CalendarEvents.FindAsync([request.EventId], cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), request.EventId);

        await _authorization.AssertMemberAsync(ev.FamilyId, request.RequestingUserId, cancellationToken);

        return CalendarMapper.ToResult(ev);
    }
}
