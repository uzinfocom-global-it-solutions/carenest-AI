using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class HealthAdvisoryWorker : PeriodicBackgroundService
{
    public HealthAdvisoryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthAdvisoryWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string ServiceName => nameof(HealthAdvisoryWorker);
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var localHour = DateTime.Now.Hour;
        if (localHour < 7 || localHour >= 22) return;

        var db = services.GetRequiredService<IApplicationDbContext>();
        var contextService = services.GetRequiredService<IProactiveContextService>();
        var aiClient = services.GetRequiredService<IAiClient>();
        var voiceService = services.GetRequiredService<IVoiceActionService>();
        var chatSync = services.GetRequiredService<IChatSynchronizationService>();
        var logger = services.GetRequiredService<ILogger<HealthAdvisoryWorker>>();

        var familyIds = await db.Families.Select(f => f.Id).ToListAsync(ct);

        foreach (var familyId in familyIds)
        {
            try
            {
                await ProcessFamilyAsync(
                    familyId, db, contextService, aiClient, voiceService, chatSync, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "HealthAdvisoryWorker failed for family {FamilyId}", familyId);
            }
        }
    }

    private static async Task ProcessFamilyAsync(
        int familyId,
        IApplicationDbContext db,
        IProactiveContextService contextService,
        IAiClient aiClient,
        IVoiceActionService voiceService,
        IChatSynchronizationService chatSync,
        ILogger logger,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var hourKey = $"health-advisory:{familyId}:{now:yyyyMMddHH}";

        var alreadySent = await db.VoiceActions
            .AnyAsync(a => a.FamilyId == familyId
                           && a.IdempotencyKey != null
                           && a.IdempotencyKey.StartsWith($"health-advisory:{familyId}:")
                           && a.CreatedAt >= now.AddMinutes(-61), ct);
        if (alreadySent) return;

        var ctx = await contextService.BuildContextAsync(familyId, ct);

        if (ctx.Children.Count == 0) return;

        var prompt = BuildPrompt(ctx, now);
        string advice;
        try
        {
            var result = await aiClient.CompleteAsync(new AiCompletionRequest(
                Prompt: prompt,
                SystemPrompt: SystemPrompt,
                Temperature: 0.6), ct);
            advice = result.Content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HealthAdvisoryWorker: LLM call failed for family {FamilyId} — using fallback", familyId);
            advice = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(advice))
            advice = BuildFallbackAdvice(ctx, now);

        var memberUserIds = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        bool anyCreated = false;
        foreach (var userId in memberUserIds)
        {
            var idemKey = VoiceActionDeduplicationService.BuildKey(
                userId, VoiceActionType.ProactiveRecommendation, hourKey, now);

            var created = await voiceService.CreateAsync(
                familyId, userId,
                VoiceActionType.ProactiveRecommendation,
                advice,
                NotificationPriority.Normal,
                requiresConfirmation: false,
                escalationEnabled: false,
                scheduledAt: null,
                metadata: null,
                idempotencyKey: idemKey,
                ct);

            if (created is not null) anyCreated = true;
        }

        if (anyCreated)
        {
            await chatSync.SaveProactiveMessageAsync(
                familyId,
                advice,
                ProactiveChatMessageType.HealthAlert,
                correlationId: hourKey,
                metadata: new Dictionary<string, string>
                {
                    ["hour"] = now.ToString("HH:00"),
                    ["aqiIndex"] = ctx.Weather?.AqiIndex?.ToString() ?? "",
                    ["tempC"] = ctx.Weather?.TemperatureCelsius.ToString("0") ?? "",
                },
                ct: ct);

            logger.LogInformation(
                "Health advisory sent for family {FamilyId} at {Hour}:00",
                familyId, now.Hour);
        }
    }

    private const string SystemPrompt =
        """
        Ты — заботливый ИИ-помощник для семьи с детьми. Твоя задача — давать краткие,
        практичные советы по здоровью и самочувствию на основе текущих условий.

        Правила:
        - Отвечай ТОЛЬКО на русском языке
        - Максимум 3 предложения, лаконично и по делу
        - Учитывай возраст и чувствительности детей
        - Если погода хорошая и всё в норме — дай позитивный совет для активности
        - Не используй markdown, только обычный текст
        - Начинай с обращения к ситуации, а не с «Привет»
        """;

    private static string BuildPrompt(FamilyProactiveContext ctx, DateTimeOffset now)
    {
        var parts = new List<string>();
        parts.Add($"Текущее время: {now.ToLocalTime():HH:mm}.");

        if (ctx.Weather is { } w)
        {
            parts.Add($"Погода: {w.ConditionSummary}, {w.TemperatureCelsius:0}°C" +
                      (w.FeelsLikeCelsius != w.TemperatureCelsius
                          ? $" (ощущается как {w.FeelsLikeCelsius:0}°C)" : "") + ".");

            if (w.AqiIndex.HasValue)
                parts.Add($"Индекс качества воздуха (AQI): {w.AqiIndex} — {AqiDescription(w.AqiIndex.Value)}.");

            if (w.IsRaining || w.RainChancePercent > 50)
                parts.Add($"Вероятность дождя: {w.RainChancePercent}%.");
        }

        if (ctx.Children.Count > 0)
        {
            foreach (var child in ctx.Children)
            {
                var sens = child.Sensitivities.Count > 0
                    ? $", чувствителен к: {string.Join(", ", child.Sensitivities)}"
                    : "";
                parts.Add($"Ребёнок: {child.Name}{sens}.");
            }
        }

        if (ctx.TodayMedications.Count > 0)
        {
            var medsStr = string.Join("; ", ctx.TodayMedications.Select(
                m => $"{m.MedicationName} ({m.Dosage}) для {m.ChildName} в {m.ScheduleTime:HH:mm}"));
            parts.Add($"Лекарства сегодня: {medsStr}.");
        }

        if (ctx.TodayEvents.Count > 0)
        {
            var evStr = string.Join(", ", ctx.TodayEvents.Take(2).Select(
                e => $"{e.Title} ({e.StartTime.ToLocalTime():HH:mm})"));
            parts.Add($"Предстоящие события: {evStr}.");
        }

        parts.Add("Дай один персональный совет по здоровью или самочувствию семьи для текущего момента дня.");

        return string.Join(" ", parts);
    }

    private static string BuildFallbackAdvice(FamilyProactiveContext ctx, DateTimeOffset now)
    {
        if (ctx.Children.Count == 0) return string.Empty;

        var parts = new List<string>();

        var nextEvent = ctx.TodayEvents
            .Where(e => e.StartTime > now && e.StartTime <= now.AddHours(3))
            .OrderBy(e => e.StartTime)
            .FirstOrDefault();

        if (nextEvent is not null)
        {
            var minsUntil = (int)(nextEvent.StartTime - now).TotalMinutes;
            var timeStr   = minsUntil < 60
                ? $"через {minsUntil} мин"
                : $"в {nextEvent.StartTime.ToLocalTime():HH:mm}";
            var who = nextEvent.ChildName != "Семья"
                ? $" для {nextEvent.ChildName}" : "";
            parts.Add($"Скоро{who}: «{nextEvent.Title}» — {timeStr}.");
        }

        var upcoming = ctx.TodayMedications
            .Select(m => (med: m, t: new DateTimeOffset(now.Date.Add(m.ScheduleTime.ToTimeSpan()), now.Offset)))
            .Where(x => x.t > now.AddMinutes(5) && x.t <= now.AddHours(2))
            .OrderBy(x => x.t)
            .Take(2)
            .ToList();

        if (upcoming.Count > 0)
        {
            var medStr = string.Join("; ", upcoming.Select(x =>
                $"{x.med.MedicationName} для {x.med.ChildName} в {x.t.ToLocalTime():HH:mm}"));
            parts.Add($"Ближайшие лекарства: {medStr}.");
        }

        if (ctx.Weather is { } w)
        {
            foreach (var child in ctx.Children)
            {
                var s = child.Sensitivities;
                if (w.AqiIndex > 100 && s.Any(x =>
                    x.Contains("воздух", StringComparison.OrdinalIgnoreCase) ||
                    x.Contains("дыхание", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"AQI {w.AqiIndex} — {child.Name} чувствителен к воздуху, лучше оставаться дома.");
                    break;
                }
                if (w.TemperatureCelsius < 3 && s.Any(x =>
                    x.Contains("холод", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"Холодно ({w.TemperatureCelsius:0}°C) — {child.Name} чувствителен, одевайте теплее.");
                    break;
                }
                if (w.TemperatureCelsius > 32 && s.Any(x =>
                    x.Contains("жара", StringComparison.OrdinalIgnoreCase) ||
                    x.Contains("heat", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"Жара {w.TemperatureCelsius:0}°C — {child.Name} чувствителен, давайте воду каждые 30 мин.");
                    break;
                }
            }

            if (parts.Count == 0)
            {
                var allNames = string.Join(" и ", ctx.Children.Select(c => c.Name));
                if (w.AqiIndex > 150)
                    parts.Add($"Качество воздуха опасное (AQI {w.AqiIndex}). Ограничьте прогулки {allNames}.");
                else if (w.IsRaining || w.RainChancePercent > 60)
                    parts.Add($"Ожидается дождь. Не забудьте зонт для {allNames}.");
                else if (w.TemperatureCelsius > 35)
                    parts.Add($"Жара {w.TemperatureCelsius:0}°C. Давайте {allNames} больше воды.");
                else if (w.TemperatureCelsius < 0)
                    parts.Add($"Мороз {w.TemperatureCelsius:0}°C. Одевайте {allNames} теплее.");
            }
        }

        if (parts.Count > 0)
            return string.Join(" ", parts);

        var c0 = ctx.Children[0];
        var evCnt  = ctx.TodayEvents.Count;
        var medCnt = ctx.TodayMedications.Count;

        if (evCnt > 0 || medCnt > 0)
        {
            var sp = new List<string>();
            if (evCnt > 0)  sp.Add($"{evCnt} событий");
            if (medCnt > 0) sp.Add($"{medCnt} приёмов лекарств");
            return $"Сегодня у {c0.Name}: {string.Join(", ", sp)}. Как дела?";
        }

        var hour = now.ToLocalTime().Hour;
        return hour switch
        {
            >= 7 and < 10  => $"Доброе утро! Как самочувствие {c0.Name} сегодня?",
            >= 10 and < 13 => $"Как дела у {c0.Name} этим утром?",
            >= 13 and < 16 => $"Как прошёл день у {c0.Name}? Не забудьте про обед.",
            >= 16 and < 20 => $"Как прошёл день у {c0.Name}?",
            >= 20 and < 22 => $"Время готовить {c0.Name} ко сну. Придерживайтесь режима.",
            _ => string.Empty,
        };
    }

    private static string AqiDescription(int aqi) => aqi switch
    {
        <= 50 => "хорошее",
        <= 100 => "умеренное",
        <= 150 => "вредно для чувствительных",
        <= 200 => "вредно",
        <= 300 => "очень вредно",
        _ => "опасно",
    };
}
