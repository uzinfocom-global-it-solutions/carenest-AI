using Backend.Application.Calendar.Internal;
using Backend.Application.Calendar.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Application.Recommendations.Commands.GenerateEventRecommendation;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Calendar.Commands.CreateCalendarEvent;

public record CreateCalendarEventCommand(
    int FamilyId,
    CalendarEventDto Event,
    string RequestingUserId) : IRequest<CalendarEventResult>;

public class CreateCalendarEventCommandHandler : IRequestHandler<CreateCalendarEventCommand, CalendarEventResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;
    private readonly ISender _mediator;

    public CreateCalendarEventCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit,
        ISender mediator)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
        _mediator = mediator;
    }

    public async Task<CalendarEventResult> Handle(CreateCalendarEventCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertMemberAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        var dto = request.Event;
        var (locationType, intensity) = await CalendarEventValidation.ValidateAsync(
            _context, dto, request.FamilyId, cancellationToken);

        var ev = new CalendarEvent
        {
            FamilyId = request.FamilyId,
            ChildId = dto.ChildId,
            Title = dto.Title,
            Description = dto.Description,
            StartDatetime = dto.StartDatetime,
            EndDatetime = dto.EndDatetime,
            LocationLabel = dto.LocationLabel,
            LocationKey = dto.LocationKey,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            LocationType = locationType,
            ActivityIntensity = intensity,
            WeatherSensitive = dto.WeatherSensitive,
            ReminderMinutes = dto.ReminderMinutes ?? 15,
            Source = CalendarEventSourceEnum.Manual,
            CreatedByUserId = request.RequestingUserId,
        };

        _context.CalendarEvents.Add(ev);
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "calendar.event_created", "calendar_event",
            ev.Id, new { request.FamilyId, dto.ChildId, dto.Title }, cancellationToken);

        // If the event starts within the next hour, fire an immediate recommendation
        // so the parent gets a notification right away (instead of waiting up to 15 min
        // for the UpcomingEventRecommendationWorker tick).
        var minutesUntil = (ev.StartDatetime - DateTimeOffset.UtcNow).TotalMinutes;
        if (minutesUntil >= 0 && minutesUntil <= 60)
        {
            try
            {
                await _mediator.Send(
                    new GenerateEventRecommendationCommand(ev.Id, request.RequestingUserId),
                    cancellationToken);
            }
            catch { /* notification failures must not fail event creation */ }
        }

        return CalendarMapper.ToResult(ev);
    }
}
