using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Runs every 15 minutes. For each family, aggregates the proactive context and
/// emits VoiceActions for urgent, time-sensitive signals:
///   - High AQI + air-quality-sensitive child → inhaler reminder
///   - Rain forecast within 1h + outdoor event → umbrella reminder
///   - Sub-zero temp + cold-sensitive child → warm clothes reminder
///   - High UV + UV-sensitive child → sunscreen reminder
///
/// Uses idempotency keys so the same alert doesn't repeat within the same hour.
/// </summary>
internal sealed class ProactiveVoiceWorker : PeriodicBackgroundService
{
    public ProactiveVoiceWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<ProactiveVoiceWorker> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override string ServiceName => nameof(ProactiveVoiceWorker);
    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var contextService = services.GetRequiredService<IProactiveContextService>();
        var voiceService = services.GetRequiredService<IVoiceActionService>();
        var chatSync = services.GetRequiredService<IChatSynchronizationService>();
        var logger = services.GetRequiredService<ILogger<ProactiveVoiceWorker>>();

        var familyIds = await db.Families
            .Select(f => f.Id)
            .ToListAsync(ct);

        foreach (var familyId in familyIds)
        {
            try
            {
                await AnalyzeFamilyAsync(familyId, contextService, voiceService, chatSync, db, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ProactiveVoiceWorker failed for family {FamilyId}", familyId);
            }
        }
    }

    private static async Task AnalyzeFamilyAsync(
        int familyId,
        IProactiveContextService contextService,
        IVoiceActionService voiceService,
        IChatSynchronizationService chatSync,
        IApplicationDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var ctx = await contextService.BuildContextAsync(familyId, ct);

        var members = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (members.Count == 0) return;

        var alerts = BuildAlerts(ctx);
        if (alerts.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        foreach (var (text, priority, sourceKey) in alerts)
        {
            bool anyCreated = false;
            foreach (var userId in members)
            {
                var idemKey = VoiceActionDeduplicationService.BuildKey(
                    userId, VoiceActionType.ProactiveRecommendation, sourceKey, now);

                var created = await voiceService.CreateAsync(
                    familyId, userId,
                    VoiceActionType.ProactiveRecommendation,
                    text,
                    priority,
                    requiresConfirmation: false,
                    escalationEnabled: false,
                    scheduledAt: null,
                    metadata: null,
                    idempotencyKey: idemKey,
                    ct);

                if (created is not null)
                {
                    logger.LogInformation(
                        "Proactive alert created for family {FamilyId} user {UserId}: {Source}",
                        familyId, userId, sourceKey);
                    anyCreated = true;
                }
            }

            // Save to chat only once per family per alert (if at least one VoiceAction was created)
            if (anyCreated)
            {
                var msgType = ResolveProactiveChatMessageType(sourceKey);
                await chatSync.SaveProactiveMessageAsync(
                    familyId,
                    text,
                    msgType,
                    correlationId: $"{sourceKey}:{now:yyyyMMddHH}",
                    ct: ct);
            }
        }
    }

    private static ProactiveChatMessageType ResolveProactiveChatMessageType(string sourceKey) => sourceKey switch
    {
        var k when k.Contains("aqi") => ProactiveChatMessageType.AQIAlert,
        var k when k.Contains("pollen") => ProactiveChatMessageType.PollenAlert,
        var k when k.Contains("weather") => ProactiveChatMessageType.WeatherAlert,
        _ => ProactiveChatMessageType.ProactiveRecommendation,
    };

    private static List<(string Text, NotificationPriority Priority, string SourceKey)> BuildAlerts(
        FamilyProactiveContext ctx)
    {
        var alerts = new List<(string, NotificationPriority, string)>();
        var w = ctx.Weather;
        if (w is null) return alerts;

        // Rain → umbrella
        if (w.IsRaining || w.RainChancePercent > 60)
            alerts.Add(("Сегодня ожидается дождь. Не забудьте взять зонт.",
                NotificationPriority.Normal, "weather:rain-umbrella"));

        // High AQI → air-sensitive child
        if (w.AqiIndex > 100 && ctx.Children.Any(c => c.Sensitivities.Contains("качество воздуха")))
            alerts.Add(($"Высокий уровень загрязнения воздуха (AQI {w.AqiIndex}). " +
                        "Чувствительному ребёнку может понадобиться ингалятор.",
                NotificationPriority.High, "weather:aqi-inhaler"));

        // Very high AQI → stay inside warning
        if (w.AqiIndex > 150)
            alerts.Add(($"Критический AQI ({w.AqiIndex}). Рекомендуется оставаться дома.",
                NotificationPriority.High, "weather:aqi-critical"));

        // Cold weather + cold-sensitive child
        if (w.TemperatureCelsius < 5 && ctx.Children.Any(c => c.Sensitivities.Contains("холод")))
            alerts.Add(($"Сегодня холодно ({w.TemperatureCelsius:0}°C). " +
                        "Оденьте чувствительного ребёнка теплее.",
                NotificationPriority.Normal, "weather:cold-warmclothes"));

        // High UV + UV-sensitive child
        // (UV index not currently in WeatherSnapshot, but we can add future logic here)

        return alerts;
    }
}
