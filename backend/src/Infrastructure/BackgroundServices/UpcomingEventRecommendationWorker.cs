using Backend.Application.Recommendations.Commands.GenerateEventRecommendation;
using Backend.Domain.Enums;
using Backend.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

/// <summary>
/// Every UpcomingEventRecommendationInterval, finds upcoming calendar events that start within the
/// next hour and generates one recommendation per event (deduped by CalendarEventId so the user
/// never gets the same nudge twice). Runs in addition to ProactiveAnalysisWorker — that one looks
/// at children/weather/routines, this one is event-window-only.
/// </summary>
internal sealed class UpcomingEventRecommendationWorker : PeriodicBackgroundService
{
    private const string SystemActorId = "system:event-window";
    private readonly TimeSpan _interval;
    private readonly TimeSpan _lookahead;

    public UpcomingEventRecommendationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<UpcomingEventRecommendationWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = options.Value.UpcomingEventRecommendationInterval;
        _lookahead = options.Value.UpcomingEventLookahead;
    }

    protected override string ServiceName => nameof(UpcomingEventRecommendationWorker);
    protected override TimeSpan Interval => _interval;

    protected override async Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken ct)
    {
        var context = scopedServices.GetRequiredService<ApplicationDbContext>();
        var mediator = scopedServices.GetRequiredService<ISender>();
        var logger = scopedServices.GetRequiredService<ILogger<UpcomingEventRecommendationWorker>>();

        var now = DateTimeOffset.UtcNow;
        var horizon = now.Add(_lookahead);

        // Pull upcoming events whose family still has at least one active member.
        var upcomingIds = await context.CalendarEvents
            .Where(e => e.StartDatetime >= now
                     && e.StartDatetime <= horizon
                     && e.Family.Members.Any(m => m.Status == FamilyMemberStatusEnum.Active))
            .Where(e => !context.Recommendations.Any(r => r.CalendarEventId == e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);

        if (upcomingIds.Count == 0)
        {
            logger.LogDebug("UpcomingEventRecommendationWorker: nothing to do.");
            return;
        }

        var generated = 0;
        foreach (var eventId in upcomingIds)
        {
            try
            {
                var rec = await mediator.Send(
                    new GenerateEventRecommendationCommand(eventId, SystemActorId), ct);
                if (rec is not null) generated++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate recommendation for event {EventId}", eventId);
            }
        }

        logger.LogInformation(
            "UpcomingEventRecommendationWorker: scanned {Scanned} events, generated {Generated} recs (window {Lookahead})",
            upcomingIds.Count, generated, _lookahead);
    }
}
