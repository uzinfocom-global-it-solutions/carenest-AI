using Backend.Application.Recommendations.Commands.GenerateRecommendations;
using Backend.Domain.Enums;
using Backend.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

/// Walks every active child and generates contextual recommendations.
/// Recommendation generation is the only responsibility of this worker.
///
/// Proactive AI chat messages are NOT sent here — they flow exclusively through
/// ContinuousContextAnalysisWorker to enforce the single orchestration pipeline rule.
internal sealed class ProactiveAnalysisWorker : PeriodicBackgroundService
{
    private const string SystemActorId = "system:proactive-analysis";
    private readonly TimeSpan _interval;

    public ProactiveAnalysisWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<ProactiveAnalysisWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = options.Value.ProactiveAnalysisInterval;
    }

    protected override string ServiceName => nameof(ProactiveAnalysisWorker);
    protected override TimeSpan Interval => _interval;

    protected override async Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken ct)
    {
        var context  = scopedServices.GetRequiredService<ApplicationDbContext>();
        var mediator = scopedServices.GetRequiredService<ISender>();
        var logger   = scopedServices.GetRequiredService<ILogger<ProactiveAnalysisWorker>>();

        var children = await context.Children
            .Where(c => c.Family.Members.Any(m => m.Status == FamilyMemberStatusEnum.Active))
            .Select(c => new { c.Id, c.FamilyId })
            .ToListAsync(ct);

        foreach (var child in children)
        {
            try
            {
                await mediator.Send(
                    new GenerateRecommendationsCommand(child.Id, SystemActorId), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Proactive analysis failed for child {ChildId}", child.Id);
            }
        }

        logger.LogDebug("ProactiveAnalysis iteration complete: {Count} children", children.Count);
    }
}
