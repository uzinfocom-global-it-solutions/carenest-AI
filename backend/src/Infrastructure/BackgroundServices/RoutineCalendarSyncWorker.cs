using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class RoutineCalendarSyncWorker : PeriodicBackgroundService
{
    private readonly TimeSpan _interval;
    private readonly int _horizonDays;

    public RoutineCalendarSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<RoutineCalendarSyncWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = options.Value.RoutineCalendarSyncInterval;
        _horizonDays = options.Value.RoutineCalendarHorizonDays;
    }

    protected override string ServiceName => nameof(RoutineCalendarSyncWorker);
    protected override TimeSpan Interval => _interval;

    protected override async Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var context = scopedServices.GetRequiredService<ApplicationDbContext>();
        await SyncAsync(context, _horizonDays, cancellationToken);
    }

    public static async Task<int> SyncAsync(
        ApplicationDbContext context,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var horizonEnd = today.AddDays(horizonDays);

        var routines = await context.ChildRoutines
            .Where(r => r.Active && r.RepeatPattern != RepeatPatternEnum.None)
            .Include(r => r.Child)
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var routine in routines)
        {
            for (var date = today; date < horizonEnd; date = date.AddDays(1))
            {
                if (!FiresOn(routine.RepeatPattern, date))
                    continue;

                var startUtc = new DateTimeOffset(
                    date.Year, date.Month, date.Day,
                    routine.StartTime.Hour, routine.StartTime.Minute, 0, TimeSpan.Zero);

                var endTime = routine.EndTime ?? routine.StartTime.AddHours(1);
                var endUtc = new DateTimeOffset(
                    date.Year, date.Month, date.Day,
                    endTime.Hour, endTime.Minute, 0, TimeSpan.Zero);

                if (endUtc <= startUtc)
                    endUtc = endUtc.AddDays(1); // overnight (e.g. sleep)

                var alreadyExists = await context.CalendarEvents.AnyAsync(
                    e => e.ChildId == routine.ChildId
                      && e.Source == CalendarEventSourceEnum.Ai
                      && e.StartDatetime == startUtc
                      && e.Title == routine.Title,
                    cancellationToken);

                if (alreadyExists)
                    continue;

                context.CalendarEvents.Add(new CalendarEvent
                {
                    FamilyId = routine.Child.FamilyId,
                    ChildId = routine.ChildId,
                    Title = routine.Title,
                    StartDatetime = startUtc,
                    EndDatetime = endUtc,
                    LocationType = routine.LocationType,
                    ActivityIntensity = routine.ActivityIntensity,
                    WeatherSensitive = routine.WeatherSensitive,
                    Source = CalendarEventSourceEnum.Ai,
                });
                created++;
            }
        }

        if (created > 0)
            await context.SaveChangesAsync(cancellationToken);

        return created;
    }

    private static bool FiresOn(RepeatPatternEnum pattern, DateOnly date) => pattern switch
    {
        RepeatPatternEnum.Daily => true,
        RepeatPatternEnum.Weekdays => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        RepeatPatternEnum.Weekends => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        RepeatPatternEnum.Weekly => date.DayOfWeek == DayOfWeek.Monday,
        RepeatPatternEnum.None => false,
        RepeatPatternEnum.Custom => false, // custom patterns require explicit scheduling
        _ => false,
    };
}
