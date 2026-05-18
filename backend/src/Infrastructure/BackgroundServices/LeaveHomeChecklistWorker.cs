using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Runs every 5 minutes. For each active family, finds CalendarEvents or ChildRoutines
/// starting in the next 15-30 minutes. If an event is found and no checklist was already
/// dispatched today for this family, it builds and sends a leave-home checklist.
/// </summary>
internal sealed class LeaveHomeChecklistWorker : PeriodicBackgroundService
{
    public LeaveHomeChecklistWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LeaveHomeChecklistWorker> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override string ServiceName => nameof(LeaveHomeChecklistWorker);
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var contextService = services.GetRequiredService<IProactiveContextService>();
        var voiceService = services.GetRequiredService<IVoiceActionService>();
        var chatSync = services.GetRequiredService<IChatSynchronizationService>();
        var logger = services.GetRequiredService<ILogger<LeaveHomeChecklistWorker>>();

        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(15);
        var windowEnd = now.AddMinutes(30);
        var todayUtc = DateOnly.FromDateTime(now.UtcDateTime);

        var familyIds = await db.Families
            .Select(f => f.Id)
            .ToListAsync(ct);

        foreach (var familyId in familyIds)
        {
            try
            {
                await ProcessFamilyAsync(familyId, now, windowStart, windowEnd, todayUtc,
                    db, contextService, voiceService, chatSync, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LeaveHomeChecklistWorker failed for family {FamilyId}", familyId);
            }
        }
    }

    private static async Task ProcessFamilyAsync(
        int familyId,
        DateTimeOffset now,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateOnly todayUtc,
        IApplicationDbContext db,
        IProactiveContextService contextService,
        IVoiceActionService voiceService,
        IChatSynchronizationService chatSync,
        ILogger logger,
        CancellationToken ct)
    {
        // Check if a checklist was already dispatched today for this family
        var alreadyDispatched = await db.LeaveHomeChecklists
            .AnyAsync(c => c.FamilyId == familyId && c.Date == todayUtc, ct);

        if (alreadyDispatched) return;

        // Find the earliest upcoming CalendarEvent starting in the 15-30 min window
        var upcomingEvent = await db.CalendarEvents
            .Where(e => e.FamilyId == familyId
                        && e.StartDatetime >= windowStart
                        && e.StartDatetime <= windowEnd)
            .OrderBy(e => e.StartDatetime)
            .FirstOrDefaultAsync(ct);

        // Also check ChildRoutines if no calendar event found
        ChildRoutine? upcomingRoutine = null;
        if (upcomingEvent is null)
        {
            var windowStartLocal = TimeOnly.FromDateTime(windowStart.LocalDateTime);
            var windowEndLocal = TimeOnly.FromDateTime(windowEnd.LocalDateTime);

            upcomingRoutine = await db.ChildRoutines
                .Include(r => r.Child)
                .Where(r => r.Child.FamilyId == familyId
                            && r.Active
                            && r.StartTime >= windowStartLocal
                            && r.StartTime <= windowEndLocal)
                .OrderBy(r => r.StartTime)
                .FirstOrDefaultAsync(ct);
        }

        if (upcomingEvent is null && upcomingRoutine is null) return;

        // Determine childId from the triggering event
        int? triggeringChildId = upcomingEvent?.ChildId ?? upcomingRoutine?.ChildId;
        string triggerTitle = upcomingEvent?.Title ?? upcomingRoutine?.Title ?? "предстоящее событие";
        DateTimeOffset? eventStartsAt = upcomingEvent?.StartDatetime;

        // Get checklist items: family-wide + child-specific
        var checklistItems = await db.FamilyItems
            .Where(i => i.FamilyId == familyId && i.IsActive && i.IsRequired
                        && (i.ChildId == null || (triggeringChildId.HasValue && i.ChildId == triggeringChildId.Value)))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToListAsync(ct);

        // Build proactive context to get weather and medication info
        var ctx = await contextService.BuildContextAsync(familyId, ct);

        // Get medications due within next 2 hours from proactive context
        var medicationsDueSoon = ctx.TodayMedications
            .Where(m =>
            {
                var diff = (m.ScheduleTime - TimeOnly.FromDateTime(now.DateTime)).TotalMinutes;
                return diff >= 0 && diff <= 120;
            })
            .ToList();

        // Build checklist text in Russian
        var checklistText = BuildChecklistText(
            triggerTitle, eventStartsAt, checklistItems, ctx.Weather, medicationsDueSoon);

        // Get active family members for VoiceActions
        var memberUserIds = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        // Create VoiceAction for each member
        foreach (var userId in memberUserIds)
        {
            var idemKey = VoiceActionDeduplicationService.BuildKey(
                userId, VoiceActionType.Custom,
                $"leave-checklist:{familyId}:{todayUtc:yyyyMMdd}", now);

            await voiceService.CreateAsync(
                familyId, userId,
                VoiceActionType.Custom,
                checklistText,
                NotificationPriority.High,
                requiresConfirmation: false,
                escalationEnabled: false,
                scheduledAt: null,
                metadata: null,
                idempotencyKey: idemKey,
                ct);
        }

        // Save to chat via IChatSynchronizationService
        await chatSync.SaveProactiveMessageAsync(
            familyId,
            checklistText,
            ProactiveChatMessageType.LeaveHomeChecklist,
            correlationId: $"checklist:{familyId}:{todayUtc:yyyyMMdd}",
            metadata: new Dictionary<string, string>
            {
                ["triggerEventTitle"] = triggerTitle,
                ["childId"] = triggeringChildId?.ToString() ?? "",
            },
            ct: ct);

        // Record LeaveHomeChecklist in DB to prevent duplicates today
        var itemNames = checklistItems.Select(i => i.Name).ToList();
        db.LeaveHomeChecklists.Add(new LeaveHomeChecklist
        {
            FamilyId = familyId,
            Date = todayUtc,
            ItemsJson = JsonSerializer.Serialize(itemNames),
            TriggerEventTitle = triggerTitle,
            EventStartsAt = eventStartsAt,
            DispatchedAt = now,
        });

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Leave-home checklist dispatched for family {FamilyId}, trigger='{Trigger}', items={Count}",
            familyId, triggerTitle, checklistItems.Count);
    }

    private static string BuildChecklistText(
        string triggerTitle,
        DateTimeOffset? eventStartsAt,
        List<FamilyItem> checklistItems,
        ProactiveWeatherContext? weather,
        List<MedicationContext> medicationsDue)
    {
        var parts = new List<string>();

        var timeStr = eventStartsAt.HasValue
            ? $" в {eventStartsAt.Value.ToLocalTime():HH:mm}"
            : "";
        parts.Add($"Скоро{timeStr} — {triggerTitle}. Не забудьте взять с собой:");

        // Add checklist items grouped by category
        if (checklistItems.Count > 0)
        {
            var grouped = checklistItems
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var categoryLabel = group.Key switch
                {
                    FamilyItemCategory.Medication => "Лекарства",
                    FamilyItemCategory.School => "Школа",
                    FamilyItemCategory.Sport => "Спорт",
                    FamilyItemCategory.Weather => "Для погоды",
                    FamilyItemCategory.Hydration => "Напитки",
                    FamilyItemCategory.General => "Общее",
                    _ => "Прочее",
                };
                var categoryItems = group.Select(i => i.Name).ToList();
                parts.Add($"[{categoryLabel}] {string.Join(", ", categoryItems)}.");
            }
        }
        else
        {
            parts.Add("Проверьте стандартный список вещей.");
        }

        // Weather-based additions
        if (weather is not null)
        {
            if (weather.IsRaining || weather.RainChancePercent > 60)
                parts.Add("Возьмите зонт — ожидается дождь.");

            if (weather.TemperatureCelsius < 5)
                parts.Add($"Оденьтесь теплее, на улице {weather.TemperatureCelsius:0}°C.");
            else if (weather.TemperatureCelsius > 28)
                parts.Add("Возьмите воду — на улице жарко.");
        }

        // Medications due soon
        foreach (var med in medicationsDue)
        {
            parts.Add($"Не забудьте лекарство: {med.MedicationName} ({med.Dosage}) для {med.ChildName}.");
        }

        parts.Add("Хорошего выхода!");
        return string.Join(" ", parts);
    }
}
