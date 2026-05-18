namespace Backend.Infrastructure.BackgroundServices;

public sealed class BackgroundWorkerOptions
{
    public const string SectionName = "BackgroundWorkers";

    public bool Enabled { get; set; } = true;

    public TimeSpan WeatherSyncInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan RoutineCalendarSyncInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ProactiveAnalysisInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan NotificationRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan UpcomingEventRecommendationInterval { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan UpcomingEventLookahead { get; set; } = TimeSpan.FromHours(1);

    public int RoutineCalendarHorizonDays { get; set; } = 7;

    public TimeOnly MorningBriefingTime { get; set; } = new TimeOnly(7, 30);
}
