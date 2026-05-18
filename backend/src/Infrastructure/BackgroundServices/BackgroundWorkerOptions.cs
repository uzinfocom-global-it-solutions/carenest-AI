namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Master switch + per-worker intervals. Bind from the "BackgroundWorkers" config section.
/// All workers share a single Enabled flag so tests can switch the entire subsystem off.
/// </summary>
public sealed class BackgroundWorkerOptions
{
    public const string SectionName = "BackgroundWorkers";

    public bool Enabled { get; set; } = true;

    public TimeSpan WeatherSyncInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan RoutineCalendarSyncInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ProactiveAnalysisInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan NotificationRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How often the upcoming-event nudger looks for new events to alert on.</summary>
    public TimeSpan UpcomingEventRecommendationInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How far ahead the upcoming-event nudger looks (events starting within this window).</summary>
    public TimeSpan UpcomingEventLookahead { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How many days of recurring calendar events the routine sync materialises ahead.</summary>
    public int RoutineCalendarHorizonDays { get; set; } = 7;

    /// <summary>Local time of day to send the morning briefing (e.g. 07:30).</summary>
    public TimeOnly MorningBriefingTime { get; set; } = new TimeOnly(7, 30);
}
