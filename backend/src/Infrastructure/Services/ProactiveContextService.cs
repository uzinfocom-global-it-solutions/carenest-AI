using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class ProactiveContextService : IProactiveContextService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ProactiveContextService> _logger;

    public ProactiveContextService(IApplicationDbContext db, ILogger<ProactiveContextService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FamilyProactiveContext> BuildContextAsync(int familyId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        // --- Children & sensitivities ---
        var children = await _db.Children
            .Include(c => c.Sensitivity)
            .Where(c => c.FamilyId == familyId)
            .ToListAsync(ct);

        var childContexts = children.Select(c =>
        {
            var s = c.Sensitivity;
            var flags = new List<string>();
            if (s?.HeatSensitive == true) flags.Add("жара");
            if (s?.ColdSensitive == true) flags.Add("холод");
            if (s?.AirQualitySensitive == true) flags.Add("качество воздуха");
            if (s?.PollenSensitive == true) flags.Add("пыльца");
            if (s?.UvSensitive == true) flags.Add("УФ");
            if (s?.RespiratorySensitive == true) flags.Add("дыхание");
            return new ProactiveChildContext
            {
                ChildId = c.Id,
                Name = c.DisplayName,
                AgeYears = c.AgeYears,
                Sensitivities = flags,
            };
        }).ToList();

        var childIds = children.Select(c => c.Id).ToList();
        var childNameMap = children.ToDictionary(c => c.Id, c => c.DisplayName);

        // --- Weather (latest snapshot for this family's location) ---
        ProactiveWeatherContext? weatherCtx = null;
        var snap = await _db.WeatherSnapshots
            .Where(w => w.CollectedAt >= now.AddHours(-2))
            .OrderByDescending(w => w.CollectedAt)
            .FirstOrDefaultAsync(ct);

        if (snap is not null)
        {
            var aqiRaw = snap.Aqi;
            int? aqiInt = null;
            if (int.TryParse(aqiRaw, out var parsed)) aqiInt = parsed;

            var condition = snap.WeatherCondition switch
            {
                WeatherConditionEnum.Sunny => "солнечно",
                WeatherConditionEnum.Cloudy => "облачно",
                WeatherConditionEnum.Rainy => "дождь",
                WeatherConditionEnum.Snowy => "снег",
                WeatherConditionEnum.Windy => "ветрено",
                WeatherConditionEnum.Storm => "гроза",
                _ => "переменная облачность",
            };

            weatherCtx = new ProactiveWeatherContext
            {
                ConditionSummary = condition,
                TemperatureCelsius = snap.TemperatureC,
                FeelsLikeCelsius = snap.FeelsLikeC ?? snap.TemperatureC,
                RainChancePercent = snap.WeatherCondition is WeatherConditionEnum.Rainy or WeatherConditionEnum.Storm ? 80 : 0,
                AqiIndex = aqiInt,
                IsRaining = snap.WeatherCondition is WeatherConditionEnum.Rainy or WeatherConditionEnum.Storm,
            };
        }

        // --- Today's medication schedules ---
        var todayDayOfWeek = now.DayOfWeek;
        var medications = await _db.MedicationSchedules
            .Where(m => childIds.Contains(m.ChildId) && m.IsActive)
            .ToListAsync(ct);

        var todayMeds = medications
            .Where(m => IsScheduledToday(m, todayDayOfWeek))
            .Select(m => new MedicationContext
            {
                ScheduleId = m.Id,
                ChildName = childNameMap.GetValueOrDefault(m.ChildId, "Ребёнок"),
                MedicationName = m.MedicationName,
                Dosage = m.Dosage,
                ScheduleTime = m.ScheduleTime,
            })
            .OrderBy(m => m.ScheduleTime)
            .ToList();

        // --- Today's calendar events ---
        var events = await _db.CalendarEvents
            .Where(e => e.FamilyId == familyId &&
                        e.StartDatetime >= now &&
                        e.StartDatetime <= now.AddHours(24))
            .OrderBy(e => e.StartDatetime)
            .Take(10)
            .ToListAsync(ct);

        var eventContexts = events.Select(e => new CalendarEventContext
        {
            EventId = e.Id,
            ChildName = e.ChildId.HasValue && childNameMap.ContainsKey(e.ChildId.Value)
                ? childNameMap[e.ChildId.Value]
                : "Семья",
            Title = e.Title,
            StartTime = e.StartDatetime,
            Location = e.LocationLabel,
        }).ToList();

        // --- Active alerts ---
        var alerts = new List<string>();
        if (weatherCtx?.AqiIndex > 150)
            alerts.Add($"Критический AQI ({weatherCtx.AqiIndex}) — оставайтесь дома или используйте маску");
        if (weatherCtx?.AqiIndex > 100 && childContexts.Any(c => c.Sensitivities.Contains("качество воздуха")))
            alerts.Add("Высокий AQI и чувствительный к воздуху ребёнок — возьмите ингалятор");
        if (weatherCtx?.IsRaining == true)
            alerts.Add("Дождь — не забудьте зонт");
        if (weatherCtx?.TemperatureCelsius < 5 && childContexts.Any(c => c.Sensitivities.Contains("холод")))
            alerts.Add("Холодная погода и чувствительный к холоду ребёнок — тёплая одежда обязательна");

        _logger.LogDebug("ProactiveContext built for family {FamilyId}: weather={Weather}, meds={Meds}, events={Events}",
            familyId, weatherCtx?.ConditionSummary ?? "none", todayMeds.Count, eventContexts.Count);

        return new FamilyProactiveContext
        {
            FamilyId = familyId,
            AsOf = now,
            Weather = weatherCtx,
            TodayMedications = todayMeds,
            TodayEvents = eventContexts,
            Children = (IReadOnlyList<ProactiveChildContext>)childContexts,
            ActiveAlerts = alerts,
        };
    }

    private static bool IsScheduledToday(Domain.Entities.MedicationSchedule m, DayOfWeek day) =>
        m.RepeatPattern switch
        {
            "daily" => true,
            "weekdays" => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            "weekends" => day is DayOfWeek.Saturday or DayOfWeek.Sunday,
            _ => true,
        };
}
