using Backend.Application.Calendar.Internal;
using Backend.Application.Calendar.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Calendar.Commands.UpdateCalendarEvent;

public record UpdateCalendarEventCommand(
    int EventId,
    CalendarEventDto Event,
    string RequestingUserId) : IRequest<CalendarEventResult>;

public class UpdateCalendarEventCommandHandler : IRequestHandler<UpdateCalendarEventCommand, CalendarEventResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public UpdateCalendarEventCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<CalendarEventResult> Handle(UpdateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        var ev = await _context.CalendarEvents.FindAsync([request.EventId], cancellationToken)
            ?? throw new NotFoundException(nameof(CalendarEvent), request.EventId);

        await _authorization.AssertMemberAsync(ev.FamilyId, request.RequestingUserId, cancellationToken);

        var dto = request.Event;
        var (locationType, intensity) = await CalendarEventValidation.ValidateAsync(
            _context, dto, ev.FamilyId, cancellationToken);

        ev.ChildId = dto.ChildId;
        ev.Title = dto.Title;
        ev.Description = dto.Description;
        ev.StartDatetime = dto.StartDatetime;
        ev.EndDatetime = dto.EndDatetime;
        ev.LocationLabel = dto.LocationLabel;
        ev.LocationKey = dto.LocationKey;
        ev.Latitude = dto.Latitude;
        ev.Longitude = dto.Longitude;
        ev.LocationType = locationType;
        ev.ActivityIntensity = intensity;
        ev.WeatherSensitive = dto.WeatherSensitive;
        ev.ReminderMinutes = dto.ReminderMinutes ?? ev.ReminderMinutes ?? 15;
        ev.ReminderSentAt = null; // reset so reminder fires again if time changed
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "calendar.event_updated", "calendar_event",
            ev.Id, new { ev.FamilyId, dto.ChildId, dto.Title }, cancellationToken);

        return CalendarMapper.ToResult(ev);
    }
}
