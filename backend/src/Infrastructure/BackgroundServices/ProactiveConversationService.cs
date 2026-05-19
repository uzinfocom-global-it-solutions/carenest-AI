using System.Text;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Generates contextual proactive check-ins: clothing advice, hydration reminders,
/// routine prompts, and activity check-ins. Runs every 30 minutes, 7:00–22:00 local.
/// </summary>
internal sealed class ProactiveConversationService : PeriodicBackgroundService
{
    public ProactiveConversationService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProactiveConversationService> logger)
        : base(scopeFactory, logger) { }

    protected override string ServiceName => nameof(ProactiveConversationService);
    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var localHour = DateTime.Now.Hour;
        if (localHour < 7 || localHour >= 22) return;

        var db          = services.GetRequiredService<IApplicationDbContext>();
        var contextSvc  = services.GetRequiredService<IProactiveContextService>();
        var aiClient    = services.GetRequiredService<IAiClient>();
        var voiceSvc    = services.GetRequiredService<IVoiceActionService>();
        var chatSync    = services.GetRequiredService<IChatSynchronizationService>();
        var logger      = services.GetRequiredService<ILogger<ProactiveConversationService>>();

        var familyIds = await db.Families.Select(f => f.Id).ToListAsync(ct);

        foreach (var familyId in familyIds)
        {
            try
            {
                await ProcessFamilyAsync(familyId, db, contextSvc, aiClient, voiceSvc, chatSync, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ProactiveConversationService failed for family {FamilyId}", familyId);
            }
        }
    }

    private static async Task ProcessFamilyAsync(
        int familyId,
        IApplicationDbContext db,
        IProactiveContextService contextSvc,
        IAiClient aiClient,
        IVoiceActionService voiceSvc,
        IChatSynchronizationService chatSync,
        ILogger logger,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var windowKey = $"proactive-conv:{familyId}:{now:yyyyMMddHH}:{now.Minute / 30}";

        // Skip if we already sent a proactive message this 30-min window
        var alreadySent = await db.VoiceActions
            .AnyAsync(a => a.FamilyId == familyId
                       && a.IdempotencyKey != null
                       && a.IdempotencyKey.StartsWith($"proactive-conv:{familyId}:")
                       && a.CreatedAt >= now.AddMinutes(-29), ct);
        if (alreadySent) return;

        var ctx = await contextSvc.BuildContextAsync(familyId, ct);
        if (ctx.Children.Count == 0) return;

        // Don't send if there are active high-severity monitoring sessions
        // (those generate their own follow-ups)
        var hasActiveEmergency = await db.HealthMonitoringSessions
            .AnyAsync(s => s.FamilyId == familyId && !s.IsResolved
                        && (s.Severity == MonitoringSeverity.Critical || s.Severity == MonitoringSeverity.Emergency), ct);
        if (hasActiveEmergency) return;

        var triggerType = DetermineTriggerType(ctx, now);
        if (triggerType is null) return;

        string? message = null;
        try
        {
            var prompt  = BuildPrompt(ctx, now, triggerType);
            var result  = await aiClient.CompleteAsync(new AiCompletionRequest(
                Prompt: prompt,
                SystemPrompt: SystemPrompt,
                Temperature: 0.6), ct);
            message = result.Content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProactiveConversationService: LLM failed for family {FamilyId}", familyId);
        }

        if (string.IsNullOrWhiteSpace(message))
            message = BuildFallback(ctx, now, triggerType);

        if (string.IsNullOrWhiteSpace(message)) return;

        var memberIds = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        foreach (var uid in memberIds)
        {
            await voiceSvc.CreateAsync(
                familyId, uid,
                VoiceActionType.ProactiveRecommendation,
                message,
                NotificationPriority.Normal,
                requiresConfirmation: false,
                escalationEnabled: false,
                scheduledAt: null,
                metadata: null,
                idempotencyKey: $"{windowKey}:{uid[..Math.Min(8, uid.Length)]}",
                ct);
        }

        await chatSync.SaveProactiveMessageAsync(
            familyId, message, ProactiveChatMessageType.ProactiveRecommendation,
            correlationId: windowKey,
            metadata: new Dictionary<string, string> { ["trigger"] = triggerType },
            ct: ct);

        logger.LogInformation(
            "Proactive conversation sent for family {FamilyId}, trigger={Trigger}",
            familyId, triggerType);
    }

    private const string SystemPrompt =
        """
        Ты — голосовой ИИ-ассистент CareNestAI. Задаёшь ОДИН короткий, практичный вопрос или
        даёшь ОДИН конкретный совет семье по уходу за детьми.

        ФОРМАТ: 1–2 предложения. Только русский. Без markdown, символов, скобок.
        Текст зачитывается вслух — пиши как говоришь.
        Начинай с имени ребёнка или конкретного факта.

        ТОНАЛЬНОСТЬ: Дружелюбно, как заботливый педиатр. Не навязчиво, не тревожно.
        Хорошо: "Миша, скоро прогулка — возьми куртку, сегодня только двенадцать градусов."
        Плохо: "Напоминаю вам о необходимости одеть ребёнка согласно погодным условиям."
        """;

    private static string? DetermineTriggerType(FamilyProactiveContext ctx, DateTimeOffset now)
    {
        var localHour = now.ToLocalTime().Hour;
        var w = ctx.Weather;

        // Clothing trigger: cold morning before outdoor event
        if (localHour is >= 7 and <= 9 && w is not null)
        {
            var outdoorEvent = ctx.TodayEvents
                .Where(e => e.StartTime > now && e.StartTime <= now.AddHours(3))
                .FirstOrDefault();

            if (outdoorEvent != null && (w.TemperatureCelsius < 15 || w.IsRaining))
                return "clothing";
        }

        // Hydration trigger: hot day
        if (w is { TemperatureCelsius: > 28 } && localHour is >= 10 and <= 16)
            return "hydration";

        // Medication upcoming trigger: 20 min window
        var upcomingMed = ctx.TodayMedications
            .Select(m => (med: m, t: new DateTimeOffset(now.Date.Add(m.ScheduleTime.ToTimeSpan()), now.Offset)))
            .Where(x => x.t > now.AddMinutes(5) && x.t <= now.AddMinutes(25))
            .FirstOrDefault();
        if (upcomingMed.med != null) return "medication";

        // Outdoor event upcoming with weather concern
        var soonEvent = ctx.TodayEvents
            .Where(e => e.StartTime > now && e.StartTime <= now.AddMinutes(45))
            .FirstOrDefault();
        if (soonEvent != null && w is not null && (w.AqiIndex > 100 || w.IsRaining || w.TemperatureCelsius < 5))
            return "event_weather";

        // Sleep trigger: evening
        if (localHour is >= 19 and <= 21 && ctx.Children.Any(c => c.AgeYears <= 8))
            return "sleep";

        // General wellness check (not too often — only if no other trigger)
        if (localHour is 11 or 15 && ctx.Children.Count > 0)
            return "wellness";

        return null;
    }

    private static string BuildPrompt(FamilyProactiveContext ctx, DateTimeOffset now, string trigger)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Время: {now.ToLocalTime():HH:mm}.");
        sb.AppendLine($"Триггер: {trigger}.");

        if (ctx.Weather is { } w)
        {
            sb.AppendLine($"Погода: {w.ConditionSummary}, {w.TemperatureCelsius:0}°C" +
                (w.IsRaining ? ", дождь" : "") +
                (w.AqiIndex.HasValue ? $", AQI {w.AqiIndex}" : "") + ".");
        }

        foreach (var child in ctx.Children.Take(2))
        {
            var sens = child.Sensitivities.Count > 0
                ? $", чувствителен к: {string.Join(", ", child.Sensitivities)}"
                : "";
            sb.AppendLine($"Ребёнок: {child.Name}, {child.AgeYears} лет{sens}.");
        }

        var localNow = now.ToLocalTime();

        switch (trigger)
        {
            case "clothing":
            {
                var evt = ctx.TodayEvents.Where(e => e.StartTime > now && e.StartTime <= now.AddHours(3)).FirstOrDefault();
                if (evt is not null)
                    sb.AppendLine($"Событие через {(int)(evt.StartTime - now).TotalMinutes} мин: {evt.Title}.");
                sb.AppendLine("Дай короткий конкретный совет по одежде для прогулки.");
                break;
            }
            case "hydration":
                sb.AppendLine("Жарко — дай короткое напоминание о воде для детей.");
                break;
            case "medication":
            {
                var med = ctx.TodayMedications
                    .Select(m => (med: m, t: new DateTimeOffset(now.Date.Add(m.ScheduleTime.ToTimeSpan()), now.Offset)))
                    .Where(x => x.t > now.AddMinutes(5) && x.t <= now.AddMinutes(25))
                    .OrderBy(x => x.t).First();
                var mins = (int)(med.t - now).TotalMinutes;
                sb.AppendLine($"Лекарство {med.med.MedicationName} для {med.med.ChildName} через {mins} мин. Напомни коротко.");
                break;
            }
            case "event_weather":
            {
                var evt = ctx.TodayEvents.Where(e => e.StartTime > now && e.StartTime <= now.AddMinutes(45)).FirstOrDefault();
                if (evt is not null)
                    sb.AppendLine($"Событие через {(int)(evt.StartTime - now).TotalMinutes} мин: {evt.Title}. Предупреди о погоде.");
                break;
            }
            case "sleep":
                sb.AppendLine("Вечер, скоро сон у малышей. Дай короткий совет-напоминание.");
                break;
            default:
                sb.AppendLine("Дай один короткий позитивный совет или вопрос о самочувствии детей.");
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildFallback(FamilyProactiveContext ctx, DateTimeOffset now, string trigger)
    {
        if (ctx.Children.Count == 0) return string.Empty;
        var child = ctx.Children[0];
        var localHour = now.ToLocalTime().Hour;

        return trigger switch
        {
            "clothing" when ctx.Weather is { } w =>
                $"Перед прогулкой с {child.Name} не забудьте одеться по погоде — {w.TemperatureCelsius:0}°C{(w.IsRaining ? ", дождь" : "")}.",

            "hydration" when ctx.Weather is { } w =>
                $"Жарко, {w.TemperatureCelsius:0}°C — давайте {child.Name} воду каждые 30 минут.",

            "medication" when ctx.TodayMedications.Count > 0 =>
                $"Скоро время давать {ctx.TodayMedications[0].MedicationName} для {ctx.TodayMedications[0].ChildName}.",

            "sleep" =>
                $"Время готовить {child.Name} ко сну. Спокойные занятия, без экранов за час до сна.",

            "wellness" => localHour < 13
                ? $"Как себя чувствует {child.Name} сегодня утром?"
                : $"Как прошёл день у {child.Name}?",

            _ => string.Empty,
        };
    }
}
