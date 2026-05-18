namespace Backend.Application.Common.Interfaces;

public interface IProactiveContextService
{
    Task<FamilyProactiveContext> BuildContextAsync(int familyId, CancellationToken ct = default);
}

public sealed class FamilyProactiveContext
{
    public int FamilyId { get; init; }
    public DateTimeOffset AsOf { get; init; }
    public ProactiveWeatherContext? Weather { get; init; }
    public IReadOnlyList<MedicationContext> TodayMedications { get; init; } = [];
    public IReadOnlyList<CalendarEventContext> TodayEvents { get; init; } = [];
    public IReadOnlyList<ProactiveChildContext> Children { get; init; } = [];
    public IReadOnlyList<string> ActiveAlerts { get; init; } = [];

    public string ToPromptContext()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Дата: {AsOf:dd.MM.yyyy}, время: {AsOf:HH:mm} UTC");

        if (Weather is not null)
        {
            sb.AppendLine($"Погода: {Weather.ConditionSummary}, темп {Weather.TemperatureCelsius:0}°C, " +
                          $"AQI={Weather.AqiIndex ?? 0}, ощущается как {Weather.FeelsLikeCelsius:0}°C");
            if (Weather.RainChancePercent > 40)
                sb.AppendLine($"  ⚠ Вероятность дождя {Weather.RainChancePercent}%");
            if (Weather.AqiIndex > 100)
                sb.AppendLine($"  ⚠ Высокий AQI ({Weather.AqiIndex}) — рекомендуется маска/ингалятор");
        }

        if (TodayMedications.Count > 0)
        {
            sb.AppendLine("Лекарства на сегодня:");
            foreach (var m in TodayMedications)
                sb.AppendLine($"  💊 {m.ChildName}: {m.MedicationName} {m.Dosage} в {m.ScheduleTime:HH:mm}");
        }

        if (TodayEvents.Count > 0)
        {
            sb.AppendLine("События на сегодня:");
            foreach (var e in TodayEvents)
                sb.AppendLine($"  📅 {e.ChildName}: {e.Title} в {e.StartTime:HH:mm}");
        }

        foreach (var child in Children.Where(c => c.Sensitivities.Count > 0))
        {
            sb.AppendLine($"Чувствительности {child.Name}: {string.Join(", ", child.Sensitivities)}");
        }

        foreach (var alert in ActiveAlerts)
            sb.AppendLine($"  ⚠ {alert}");

        return sb.ToString();
    }
}

public sealed class ProactiveWeatherContext
{
    public string ConditionSummary { get; init; } = string.Empty;
    public double TemperatureCelsius { get; init; }
    public double FeelsLikeCelsius { get; init; }
    public int RainChancePercent { get; init; }
    public int? AqiIndex { get; init; }
    public bool IsRaining { get; init; }
}

public sealed class MedicationContext
{
    public int ScheduleId { get; init; }
    public string ChildName { get; init; } = string.Empty;
    public string MedicationName { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public TimeOnly ScheduleTime { get; init; }
}

public sealed class CalendarEventContext
{
    public int EventId { get; init; }
    public string ChildName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public string? Location { get; init; }
}

public sealed class ProactiveChildContext
{
    public int ChildId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int AgeYears { get; init; }
    public IReadOnlyList<string> Sensitivities { get; init; } = [];
}
