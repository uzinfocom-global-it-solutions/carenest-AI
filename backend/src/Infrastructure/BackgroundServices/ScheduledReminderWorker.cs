using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Runs every 30 seconds. Picks up VoiceActions whose scheduledAt has arrived
/// (status = Pending, scheduledAt <= now) and enqueues them for immediate dispatch.
/// This powers the "remind me in X minutes" feature.
/// </summary>
internal sealed class ScheduledReminderWorker : PeriodicBackgroundService
{
    public ScheduledReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledReminderWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string ServiceName => nameof(ScheduledReminderWorker);
    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var queue = services.GetRequiredService<IVoiceActionQueue>();
        var logger = services.GetRequiredService<ILogger<ScheduledReminderWorker>>();

        var now = DateTimeOffset.UtcNow;

        var due = await db.VoiceActions
            .Where(a => a.Status == VoiceActionStatus.Pending
                        && a.ScheduledAt != null
                        && a.ScheduledAt <= now
                        && a.Type != VoiceActionType.FollowUpCheck)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var action in due)
        {
            try
            {
                action.Status = VoiceActionStatus.Queued;
                await queue.EnqueueAsync(action, ct);
                logger.LogInformation(
                    "Scheduled reminder {Id} due for user {UserId}: {Text}",
                    action.Id, action.UserId,
                    action.Text.Length > 60 ? action.Text[..60] + "…" : action.Text);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enqueue scheduled reminder {Id}", action.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
