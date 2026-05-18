using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

public sealed class EventReminderDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventReminderDispatchWorker> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public EventReminderDispatchWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EventReminderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventReminderDispatchWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "EventReminderDispatchWorker unhandled exception");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var voiceActionService = scope.ServiceProvider.GetRequiredService<IVoiceActionService>();

        var now = DateTimeOffset.UtcNow;

        // Find events whose reminder window has opened but reminder hasn't been sent yet.
        // ReminderMinutes defaults to 15 if null.
        var dueEvents = await db.CalendarEvents
            .Include(e => e.Family)
                .ThenInclude(f => f.Members.Where(m => m.Status == FamilyMemberStatusEnum.Active))
            .Where(e =>
                e.ReminderSentAt == null &&
                e.StartDatetime > now &&
                e.StartDatetime <= now.AddMinutes(e.ReminderMinutes ?? 15))
            .ToListAsync(ct);

        if (dueEvents.Count == 0) return;

        foreach (var ev in dueEvents)
        {
            var minutesBefore = ev.ReminderMinutes ?? 15;
            var minutesUntil = (int)Math.Round((ev.StartDatetime - now).TotalMinutes);

            var text = minutesUntil <= 1
                ? $"Через минуту: {ev.Title}"
                : $"Через {minutesUntil} мин: {ev.Title}";

            if (ev.Description is { Length: > 0 })
                text += $". {ev.Description}";

            var metadata = JsonSerializer.Serialize(new
            {
                title = $"📅 Напоминание о событии",
                eventId = ev.Id,
                eventTitle = ev.Title,
                startDatetime = ev.StartDatetime,
                minutesBefore,
            });

            // Idempotency key: reminder per event per day (avoids duplicate if worker runs late)
            var dayStamp = ev.StartDatetime.UtcDateTime.ToString("yyyyMMdd");
            var idempotencyKey = $"er:{ev.Id}:{dayStamp}";

            var activeMembers = ev.Family.Members
                .Where(m => m.Status == FamilyMemberStatusEnum.Active)
                .ToList();

            var dispatched = false;
            foreach (var member in activeMembers)
            {
                try
                {
                    var result = await voiceActionService.CreateAsync(
                        familyId: ev.FamilyId,
                        userId: member.UserId,
                        type: VoiceActionType.CalendarAlert,
                        text: text,
                        priority: NotificationPriority.Normal,
                        requiresConfirmation: false,
                        escalationEnabled: false,
                        scheduledAt: null,
                        metadata: metadata,
                        idempotencyKey: $"{idempotencyKey}:{member.UserId[..Math.Min(8, member.UserId.Length)]}",
                        ct: ct);

                    if (result is not null) dispatched = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create reminder VoiceAction for event {EventId} user {UserId}", ev.Id, member.UserId);
                }
            }

            if (dispatched)
            {
                // Mark reminder as sent so it doesn't fire again
                ev.ReminderSentAt = now;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "EventReminderDispatchWorker: sent reminder for event {EventId} '{Title}' ({MinutesBefore}min before) to {MemberCount} members",
                    ev.Id, ev.Title, minutesBefore, activeMembers.Count);
            }
        }
    }
}
