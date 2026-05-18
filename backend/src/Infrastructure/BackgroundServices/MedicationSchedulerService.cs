using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

public sealed class MedicationSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MedicationSchedulerService> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public MedicationSchedulerService(IServiceScopeFactory scopeFactory, ILogger<MedicationSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MedicationSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSchedulesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MedicationSchedulerService unhandled exception");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CheckSchedulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var voiceActionService = scope.ServiceProvider.GetRequiredService<IVoiceActionService>();
        var chatSync = scope.ServiceProvider.GetRequiredService<IChatSynchronizationService>();

        var now = DateTimeOffset.UtcNow;

        var schedules = await db.MedicationSchedules
            .Include(s => s.Child)
            .ThenInclude(c => c.Family)
            .ThenInclude(f => f.Members.Where(m => m.Status == FamilyMemberStatusEnum.Active))
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);
                var localNow = TimeZoneInfo.ConvertTime(now, tz);
                var localTime = TimeOnly.FromDateTime(localNow.DateTime);

                if (!ShouldTrigger(schedule, localNow, localTime)) continue;

                // Debounce: don't retrigger if already triggered within last 90 seconds
                if (schedule.LastTriggeredAt.HasValue &&
                    (now - schedule.LastTriggeredAt.Value).TotalSeconds < 90)
                    continue;

                schedule.LastTriggeredAt = now;
                await db.SaveChangesAsync(ct);

                var familyId = schedule.Child.FamilyId;
                var members = schedule.Child.Family.Members;

                var reminderText = $"Время принять {schedule.MedicationName}, доза: {schedule.Dosage}. " +
                                   $"Пожалуйста подтвердите приём лекарства.";

                foreach (var member in members)
                {
                    var metadata = JsonSerializer.Serialize(new
                    {
                        medicationScheduleId = schedule.Id,
                        childId = schedule.ChildId,
                        medicationName = schedule.MedicationName,
                        dosage = schedule.Dosage,
                    });

                    // Idempotency: same medication+user triggers at most once per hour
                    var idemKey = VoiceActionDeduplicationService.BuildKey(
                        member.UserId, VoiceActionType.MedicationReminder,
                        $"medication:{schedule.Id}", now);

                    await voiceActionService.CreateAsync(
                        familyId,
                        member.UserId,
                        VoiceActionType.MedicationReminder,
                        reminderText,
                        NotificationPriority.High,
                        schedule.RequiresConfirmation,
                        schedule.EscalationEnabled,
                        scheduledAt: null,
                        metadata,
                        idemKey,
                        ct);
                }

                // Save proactive message to family chat and emit SSE
                var slot = schedule.ScheduleTime.ToString("HHmm");
                await chatSync.SaveProactiveMessageAsync(
                    familyId,
                    reminderText,
                    ProactiveChatMessageType.MedicationReminder,
                    correlationId: $"med:{schedule.Id}:{slot}",
                    metadata: new Dictionary<string, string>
                    {
                        ["medicationScheduleId"] = schedule.Id.ToString(),
                        ["childId"] = schedule.ChildId.ToString(),
                        ["medicationName"] = schedule.MedicationName,
                    },
                    ct: ct);

                _logger.LogInformation(
                    "Medication reminder triggered: schedule {Id} '{Name}' for child {ChildId}",
                    schedule.Id, schedule.MedicationName, schedule.ChildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process medication schedule {Id}", schedule.Id);
            }
        }
    }

    private static bool ShouldTrigger(Domain.Entities.MedicationSchedule schedule, DateTimeOffset localNow, TimeOnly localTime)
    {
        // Match within 1-minute window
        var diff = Math.Abs((localTime - schedule.ScheduleTime).TotalMinutes);
        if (diff > 1 && diff < 1439) return false; // 1439 = 24h-1min (handles midnight wrap)

        return schedule.RepeatPattern switch
        {
            "daily" => true,
            "weekdays" => localNow.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            "weekends" => localNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            "custom" when schedule.CustomDays is not null =>
                JsonSerializer.Deserialize<List<string>>(schedule.CustomDays)?
                    .Contains(localNow.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase) == true,
            _ => true,
        };
    }
}
