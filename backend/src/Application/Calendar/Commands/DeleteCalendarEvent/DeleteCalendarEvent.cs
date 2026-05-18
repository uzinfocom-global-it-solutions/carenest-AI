using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Calendar.Commands.DeleteCalendarEvent;

public record DeleteCalendarEventCommand(int EventId, string RequestingUserId) : IRequest;

public class DeleteCalendarEventCommandHandler : IRequestHandler<DeleteCalendarEventCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public DeleteCalendarEventCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(DeleteCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var ev = await _context.CalendarEvents.FindAsync([request.EventId], cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), request.EventId);

        await _authorization.AssertMemberAsync(ev.FamilyId, request.RequestingUserId, cancellationToken);

        var familyId = ev.FamilyId;
        var title = ev.Title;

        _context.CalendarEvents.Remove(ev);
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "calendar.event_deleted", "calendar_event",
            request.EventId, new { familyId, title }, cancellationToken);
    }
}
