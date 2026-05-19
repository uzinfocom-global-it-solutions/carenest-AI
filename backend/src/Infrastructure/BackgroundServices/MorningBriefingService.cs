using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

public sealed class MorningBriefingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MorningBriefingService> _logger;
    private readonly TimeOnly _targetTime; // e.g. 07:30 local time

    public MorningBriefingService(
        IServiceScopeFactory scopeFactory,
        ILogger<MorningBriefingService> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _targetTime = options.Value.MorningBriefingTime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MorningBriefingService started, target time={Time}", _targetTime);

        var deliveredToday = new HashSet<int>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var utcNow = DateTimeOffset.UtcNow;

                if (utcNow.Hour == 2 && utcNow.Minute < 2)
                    deliveredToday.Clear();

                await CheckAndSendBriefingsAsync(deliveredToday, utcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MorningBriefingService iteration failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CheckAndSendBriefingsAsync(
        HashSet<int> deliveredToday,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var context = scope.ServiceProvider.GetRequiredService<IProactiveContextService>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiClient>();
        var voiceActionService = scope.ServiceProvider.GetRequiredService<IVoiceActionService>();
        var chatSync = scope.ServiceProvider.GetRequiredService<IChatSynchronizationService>();

        var families = await db.FamilyMembers
            .Where(m => m.Status == FamilyMemberStatusEnum.Active)
            .GroupBy(m => m.FamilyId)
            .Select(g => new
            {
                FamilyId = g.Key,
                Members = g.Select(m => new { m.UserId }).ToList(),
            })
            .ToListAsync(ct);

        var allUserIds = families.SelectMany(f => f.Members.Select(m => m.UserId)).ToList();
        var timezones = await db.UserSettings
            .Where(s => allUserIds.Contains(s.UserId))
            .Select(s => new { s.UserId, s.Timezone })
            .ToListAsync(ct);
        var tzMap = timezones.ToDictionary(t => t.UserId, t => t.Timezone);

        foreach (var family in families)
        {
            if (deliveredToday.Contains(family.FamilyId)) continue;

            var firstUserId = family.Members.Select(m => m.UserId).FirstOrDefault();
            if (firstUserId is null) continue;

            var tzId = tzMap.GetValueOrDefault(firstUserId, "UTC");
            var localNow = ConvertToLocal(utcNow, tzId);
            var localTime = TimeOnly.FromDateTime(localNow.DateTime);

            var diff = Math.Abs((localTime - _targetTime).TotalMinutes);
            if (diff > 1 && diff < 1439) continue; // 1439 = 24h-1m (midnight wrap)

            try
            {
                await SendBriefingToFamilyAsync(
                    family.FamilyId,
                    family.Members.Select(m => m.UserId).ToList(),
                    context, ai, voiceActionService, chatSync, utcNow, ct);

                deliveredToday.Add(family.FamilyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Morning briefing failed for family {FamilyId}", family.FamilyId);
            }
        }
    }

    private async Task SendBriefingToFamilyAsync(
        int familyId,
        List<string> memberUserIds,
        IProactiveContextService contextService,
        IAiClient ai,
        IVoiceActionService voiceActionService,
        IChatSynchronizationService chatSync,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = await contextService.BuildContextAsync(familyId, ct);

        var systemPrompt = """
            Ты — голосовой ИИ-ассистент CareNestAI. Составляешь утренний брифинг для семьи.

            ФОРМАТ: 4–6 предложений. Только русский. Текст зачитывается вслух — никаких
            символов (→ • / — ()), никаких сокращений, никаких списков. Числа произноси
            как слова когда это звучит естественно.

            СТРУКТУРА (строго в таком порядке):
            1. Приветствие с именем или "Доброе утро, семья!" — одно тёплое предложение
            2. Погода сегодня — температура, условия, стоит ли выходить
            3. Лекарства или здоровье — если есть активные назначения (самое важное)
            4. Главное событие дня — если есть, одним предложением с временем
            5. Один конкретный совет под погоду и детей (одежда, активность, питьё)
            6. Краткое пожелание хорошего дня

            ТОНАЛЬНОСТЬ: Как заботливый друг-педиатр, не диктор. Тепло, конкретно.
            Называй детей по именам. Не обобщай — говори про эту семью.
            Плохо: "Следите за здоровьем детей в связи с погодными условиями."
            Хорошо: "Ване сегодня лучше взять куртку — утром прохладно, всего двенадцать градусов."
            """;

        var prompt = $"Данные о семье на сегодня:\n{ctx.ToPromptContext()}\n\nСоставь утренний брифинг:";

        AiCompletionResult aiResult;
        try
        {
            aiResult = await ai.CompleteAsync(new AiCompletionRequest(prompt, systemPrompt), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI briefing generation failed for family {FamilyId}, using fallback", familyId);
            aiResult = new AiCompletionResult(BuildFallbackBriefing(ctx), "fallback");
        }

        var briefingText = aiResult.Content.Trim();
        if (string.IsNullOrWhiteSpace(briefingText))
            briefingText = BuildFallbackBriefing(ctx);

        var date = DateOnly.FromDateTime(now.UtcDateTime);

        foreach (var userId in memberUserIds)
        {
            var idemKey = VoiceActionDeduplicationService.BuildKey(
                userId, VoiceActionType.ProactiveRecommendation,
                $"morning-briefing:{familyId}", now);

            await voiceActionService.CreateAsync(
                familyId, userId,
                VoiceActionType.ProactiveRecommendation,
                briefingText,
                NotificationPriority.Normal,
                requiresConfirmation: false,
                escalationEnabled: false,
                scheduledAt: null,
                metadata: null,
                idempotencyKey: idemKey,
                ct);
        }

        await chatSync.SaveProactiveMessageAsync(
            familyId,
            briefingText,
            ProactiveChatMessageType.MorningBriefing,
            correlationId: $"briefing:{familyId}:{date}",
            ct: ct);

        _logger.LogInformation("Morning briefing sent to family {FamilyId}, {Members} members",
            familyId, memberUserIds.Count);
    }

    private static string BuildFallbackBriefing(FamilyProactiveContext ctx)
    {
        var parts = new List<string> { "Доброе утро!" };

        if (ctx.Weather is not null)
        {
            parts.Add($"Сегодня {ctx.Weather.ConditionSummary}, {ctx.Weather.TemperatureCelsius:0}°C.");
            if (ctx.Weather.IsRaining) parts.Add("Не забудьте зонт.");
        }

        if (ctx.TodayMedications.Count > 0)
        {
            var first = ctx.TodayMedications[0];
            parts.Add($"Напоминание: {first.MedicationName} для {first.ChildName} в {first.ScheduleTime:HH:mm}.");
        }

        if (ctx.ActiveAlerts.Count > 0)
            parts.Add(ctx.ActiveAlerts[0]);

        parts.Add("Хорошего дня!");
        return string.Join(" ", parts);
    }

    private static DateTimeOffset ConvertToLocal(DateTimeOffset utc, string tzId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTime(utc, tz);
        }
        catch
        {
            return utc;
        }
    }
}
