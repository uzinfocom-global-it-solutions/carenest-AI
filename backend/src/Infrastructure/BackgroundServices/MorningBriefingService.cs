using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Generates a personalized AI morning briefing for each family once per day.
/// Runs a tight 1-minute check loop; a per-family record tracks last delivery
/// so the briefing fires only once per calendar day in the family's local timezone.
/// </summary>
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

        // Track which families already got their briefing today (in-memory, resets on restart — safe)
        var deliveredToday = new HashSet<int>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var utcNow = DateTimeOffset.UtcNow;

                // Reset delivered set at 2:00 UTC (before any family's morning)
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

        // Get families with their member timezones
        var families = await db.FamilyMembers
            .Where(m => m.Status == FamilyMemberStatusEnum.Active)
            .GroupBy(m => m.FamilyId)
            .Select(g => new
            {
                FamilyId = g.Key,
                Members = g.Select(m => new { m.UserId }).ToList(),
            })
            .ToListAsync(ct);

        // Use UserSettings to get timezone per family (use first member's timezone)
        var allUserIds = families.SelectMany(f => f.Members.Select(m => m.UserId)).ToList();
        var timezones = await db.UserSettings
            .Where(s => allUserIds.Contains(s.UserId))
            .Select(s => new { s.UserId, s.Timezone })
            .ToListAsync(ct);
        var tzMap = timezones.ToDictionary(t => t.UserId, t => t.Timezone);

        foreach (var family in families)
        {
            if (deliveredToday.Contains(family.FamilyId)) continue;

            // Determine local time for this family
            var firstUserId = family.Members.Select(m => m.UserId).FirstOrDefault();
            if (firstUserId is null) continue;

            var tzId = tzMap.GetValueOrDefault(firstUserId, "UTC");
            var localNow = ConvertToLocal(utcNow, tzId);
            var localTime = TimeOnly.FromDateTime(localNow.DateTime);

            // Only trigger within 1-minute window of target time
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
            Ты — голосовой AI-ассистент для семьи. Составь краткое утреннее сообщение (голосовой брифинг) на русском языке.
            Говори кратко, тепло и практично. Максимум 5-6 предложений.
            Включи самое важное: погоду, лекарства, события, предупреждения.
            Начни с приветствия "Доброе утро!" и закончи пожеланием хорошего дня.
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

        // Send to each family member
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

        // Save proactive message to family chat and emit SSE
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
