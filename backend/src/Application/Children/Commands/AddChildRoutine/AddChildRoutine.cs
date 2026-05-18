using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Children.Commands.AddChildRoutine;

public record AddChildRoutineCommand(
    int ChildId,
    ChildRoutineDto Routine,
    string RequestingUserId) : IRequest<ChildRoutineResult>;

public class AddChildRoutineCommandHandler : IRequestHandler<AddChildRoutineCommand, ChildRoutineResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public AddChildRoutineCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<ChildRoutineResult> Handle(AddChildRoutineCommand request, CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        var dto = request.Routine;

        if (!Enum.TryParse<RoutineTypeEnum>(dto.RoutineType, ignoreCase: true, out var routineType))
            throw new ValidationException([new("routineType", $"Invalid routine type '{dto.RoutineType}'.")]);

        if (!Enum.TryParse<RepeatPatternEnum>(dto.RepeatPattern, ignoreCase: true, out var repeat))
            throw new ValidationException([new("repeatPattern", $"Invalid repeat pattern '{dto.RepeatPattern}'.")]);

        if (!Enum.TryParse<LocationTypeEnum>(dto.LocationType, ignoreCase: true, out var locationType))
            throw new ValidationException([new("locationType", $"Invalid location type '{dto.LocationType}'.")]);

        if (!Enum.TryParse<ActivityIntensityEnum>(dto.ActivityIntensity, ignoreCase: true, out var intensity))
            throw new ValidationException([new("activityIntensity", $"Invalid activity intensity '{dto.ActivityIntensity}'.")]);

        if (dto.EndTime.HasValue && dto.EndTime.Value <= dto.StartTime)
            throw new ValidationException([new("endTime", "End time must be after start time.")]);

        var routine = new ChildRoutine
        {
            ChildId = request.ChildId,
            Title = dto.Title,
            RoutineType = routineType,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RepeatPattern = repeat,
            LocationType = locationType,
            ActivityIntensity = intensity,
            WeatherSensitive = dto.WeatherSensitive,
        };

        _context.ChildRoutines.Add(routine);
        await _context.SaveChangesAsync(cancellationToken);

        // Materialise the new routine into the calendar immediately for the next 7 days,
        // so the parent sees it on the Plan screen without waiting for the periodic sync.
        await MaterialiseAsync(_context, routine, child.FamilyId, horizonDays: 7, cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "child.routine_added", "child_routine", routine.Id,
            new { dto.Title, routineType = dto.RoutineType }, cancellationToken);

        return new ChildRoutineResult(routine.Id, routine.ChildId, routine.Title,
            routine.RoutineType.ToString(), routine.StartTime, routine.Active);
    }

    private static async Task MaterialiseAsync(
        IApplicationDbContext context,
        ChildRoutine routine,
        int familyId,
        int horizonDays,
        CancellationToken ct)
    {
        if (routine.RepeatPattern == RepeatPatternEnum.None) return;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var horizonEnd = today.AddDays(horizonDays);
        var created = false;

        for (var date = today; date < horizonEnd; date = date.AddDays(1))
        {
            if (!FiresOn(routine.RepeatPattern, date)) continue;

            var startUtc = new DateTimeOffset(
                date.Year, date.Month, date.Day,
                routine.StartTime.Hour, routine.StartTime.Minute, 0, TimeSpan.Zero);
            var endTime = routine.EndTime ?? routine.StartTime.AddHours(1);
            var endUtc = new DateTimeOffset(
                date.Year, date.Month, date.Day,
                endTime.Hour, endTime.Minute, 0, TimeSpan.Zero);
            if (endUtc <= startUtc) endUtc = endUtc.AddDays(1);

            var alreadyExists = await context.CalendarEvents.AnyAsync(
                e => e.ChildId == routine.ChildId
                  && e.Source == CalendarEventSourceEnum.Ai
                  && e.StartDatetime == startUtc
                  && e.Title == routine.Title,
                ct);
            if (alreadyExists) continue;

            context.CalendarEvents.Add(new CalendarEvent
            {
                FamilyId = familyId,
                ChildId = routine.ChildId,
                Title = routine.Title,
                StartDatetime = startUtc,
                EndDatetime = endUtc,
                LocationType = routine.LocationType,
                ActivityIntensity = routine.ActivityIntensity,
                WeatherSensitive = routine.WeatherSensitive,
                Source = CalendarEventSourceEnum.Ai,
            });
            created = true;
        }

        if (created) await context.SaveChangesAsync(ct);
    }

    private static bool FiresOn(RepeatPatternEnum pattern, DateOnly date) => pattern switch
    {
        RepeatPatternEnum.Daily => true,
        RepeatPatternEnum.Weekdays => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        RepeatPatternEnum.Weekends => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        RepeatPatternEnum.Weekly => date.DayOfWeek == DayOfWeek.Monday,
        RepeatPatternEnum.None => false,
        RepeatPatternEnum.Custom => false,
        _ => false,
    };
}
