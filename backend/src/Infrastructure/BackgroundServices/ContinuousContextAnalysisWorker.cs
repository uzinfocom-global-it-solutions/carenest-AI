using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class ContinuousContextAnalysisWorker : PeriodicBackgroundService
{
    private const int MaxConcurrency = 4;

    public ContinuousContextAnalysisWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ContinuousContextAnalysisWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string ServiceName => nameof(ContinuousContextAnalysisWorker);
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var familyIds = await db.Families.Select(f => f.Id).ToListAsync(ct);

        var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var logger = services.GetRequiredService<ILogger<ContinuousContextAnalysisWorker>>();

        var tasks = familyIds.Select(async familyId =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
                await RunFamilyOrchestrationAsync(familyId, scope.ServiceProvider, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Orchestration failed for family {FamilyId}", familyId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static async Task RunFamilyOrchestrationAsync(
        int familyId, IServiceProvider services, CancellationToken ct)
    {
        var db             = services.GetRequiredService<IApplicationDbContext>();
        var contextSvc     = services.GetRequiredService<IProactiveContextService>();
        var decisionEngine = services.GetRequiredService<IAiDecisionEngine>();
        var monitoringSvc  = services.GetRequiredService<IHealthMonitoringService>();
        var predictiveSvc  = services.GetRequiredService<IPredictiveHealthRiskService>();
        var priorityEngine = services.GetRequiredService<IAiPriorityEngine>();
        var memorySvc      = services.GetRequiredService<IAIMemoryService>();
        var voiceSvc       = services.GetRequiredService<IVoiceActionService>();
        var chatSync       = services.GetRequiredService<IChatSynchronizationService>();
        var sse            = services.GetRequiredService<ISseConnectionManager>();
        var logger         = services.GetRequiredService<ILogger<ContinuousContextAnalysisWorker>>();

        var memberIds = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (memberIds.Count == 0) return;

        var context  = await contextSvc.BuildContextAsync(familyId, ct);
        var sessions = await monitoringSvc.GetActiveSessionsForFamilyAsync(familyId, ct);

        var familyMemory = await db.AIMemories
            .Where(m => m.FamilyId == familyId && (m.ExpiresAt == null || m.ExpiresAt > DateTimeOffset.UtcNow))
            .OrderByDescending(m => m.RelevanceScore)
            .Take(20)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            var child = context.Children.FirstOrDefault(c => c.ChildId == session.ChildId);
            var prediction = await predictiveSvc.PredictRiskAsync(
                session, child, context.Weather, familyMemory, ct);

            if (prediction.PredictedRiskScore != session.PredictedRiskScore ||
                prediction.PredictedEscalationAt != session.PredictedEscalationAt)
            {
                await monitoringSvc.UpdatePredictedRiskAsync(
                    session.Id, prediction.PredictedRiskScore, prediction.PredictedEscalationAt, ct);

                await PublishSseAsync(sse, memberIds, "risk_score_updated", new
                {
                    familyId,
                    sessionId             = session.Id,
                    riskScore             = session.RiskScore,
                    predictedRiskScore    = prediction.PredictedRiskScore,
                    predictedSeverity     = prediction.PredictedSeverity.ToString(),
                    escalationProbability = prediction.EscalationProbability,
                    predictedEscalationAt = prediction.PredictedEscalationAt,
                }, ct);
            }

            var newPhase = DetermineLifecyclePhase(session, prediction);
            if (newPhase != session.LifecyclePhase)
            {
                await monitoringSvc.UpdateLifecyclePhaseAsync(session.Id, newPhase, ct);
                await PublishSseAsync(sse, memberIds, "monitoring_plan_updated", new
                {
                    familyId,
                    sessionId      = session.Id,
                    lifecyclePhase = newPhase.ToString(),
                    previousPhase  = session.LifecyclePhase.ToString(),
                }, ct);
            }

            if (prediction.EscalationProbability > 0.6 && prediction.TimeToEscalationMinutes is <= 90)
            {
                await PublishSseAsync(sse, memberIds, "predictive_risk_detected", new
                {
                    familyId,
                    sessionId           = session.Id,
                    issueType           = session.IssueType.ToString(),
                    childId             = session.ChildId,
                    escalationProb      = prediction.EscalationProbability,
                    predictedSeverity   = prediction.PredictedSeverity.ToString(),
                    timeToEscalationMin = prediction.TimeToEscalationMinutes,
                    riskFactors         = prediction.RiskFactors,
                }, ct);
            }
        }

        var result  = await decisionEngine.AnalyzeAsync(context, sessions, ct);
        var chainId = result.DecisionChainId ?? Guid.NewGuid().ToString("N")[..16];

        foreach (var upd in result.SessionUpdates)
        {
            if (upd.ShouldResolve)
            {
                await monitoringSvc.ResolveSessionAsync(
                    upd.SessionId, upd.ResolutionSummary ?? "Resolved by AI analysis.", ct);

                await memorySvc.RecordObservationAsync(
                    familyId,
                    $"resolved:{upd.SessionId}",
                    $"session {upd.SessionId} resolved autonomously",
                    AIMemoryType.SymptomHistory, ct);

                await PublishSseAsync(sse, memberIds, "monitoring_session_updated", new
                {
                    familyId, sessionId = upd.SessionId, isResolved = true, chainId,
                }, ct);
            }
            else if (upd.NewRiskScore.HasValue)
            {
                var prevSession = sessions.FirstOrDefault(s => s.Id == upd.SessionId);
                bool escalated  = upd.NewSeverity.HasValue &&
                                  prevSession is not null &&
                                  upd.NewSeverity > prevSession.Severity;

                await monitoringSvc.UpdateSessionRiskAsync(
                    upd.SessionId,
                    new RiskAssessment(
                        upd.NewRiskScore.Value,
                        upd.NewSeverity ?? MonitoringSeverity.Low,
                        upd.NewEscalationLevel ?? MonitoringEscalationLevel.None,
                        [], 30),
                    null, ct);

                if (escalated)
                {
                    await PublishSseAsync(sse, memberIds, "monitoring_escalated", new
                    {
                        familyId,
                        sessionId   = upd.SessionId,
                        newSeverity = upd.NewSeverity?.ToString(),
                        riskScore   = upd.NewRiskScore,
                        chainId,
                        childName   = prevSession?.Child?.DisplayName,
                    }, ct);

                    logger.LogWarning(
                        "Monitoring escalated: session={SessionId} family={FamilyId} severity={Severity}",
                        upd.SessionId, familyId, upd.NewSeverity);
                }
            }
        }

        var executedThisCycle = new HashSet<string>();

        foreach (var decision in result.Decisions)
        {
            var cycleKey = $"{decision.Type}:{decision.TargetChildId}:{decision.RelatedSessionId}";

            if (executedThisCycle.Contains(cycleKey))
            {
                logger.LogDebug(
                    "In-cycle duplicate suppressed: {Type} family={FamilyId}", decision.Type, familyId);
                continue;
            }

            var priorityDecision = await priorityEngine.EvaluateAsync(
                familyId, decision.Type, decision.Priority,
                decision.TargetChildId, decision.RelatedSessionId, ct);

            if (priorityDecision.ShouldSuppress)
            {
                logger.LogDebug(
                    "Priority engine suppressed {Type} family={FamilyId}: {Reason}",
                    decision.Type, familyId, priorityDecision.SuppressReason);

                await priorityEngine.RecordDecisionAsync(
                    familyId, decision.Type, decision.Message,
                    decision.Priority, decision.TargetChildId, decision.RelatedSessionId,
                    chainId, idempotencyKey: null,
                    contextSnapshot: null,
                    escalationRationale: priorityDecision.SuppressReason,
                    metadata: null,
                    executed: false, ct);

                continue;
            }

            executedThisCycle.Add(cycleKey);

            var escalationRationale = decision.Metadata?.GetValueOrDefault("severity");

            foreach (var userId in memberIds)
            {
                try
                {
                    await voiceSvc.CreateAsync(
                        familyId, userId, decision.ActionType,
                        decision.Message, decision.Priority,
                        requiresConfirmation: false,
                        escalationEnabled: decision.Priority >= NotificationPriority.High,
                        scheduledAt: null,
                        metadata: decision.Metadata is not null
                            ? JsonSerializer.Serialize(decision.Metadata) : null,
                        idempotencyKey: $"cca:{chainId}:{decision.Type}:{userId[..Math.Min(8, userId.Length)]}",
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create VoiceAction for user {UserId}", userId);
                }
            }

            var chatMessageType = decision.Type switch
            {
                AiDecisionType.EscalationAlert    => ProactiveChatMessageType.EscalationAlert,
                AiDecisionType.HealthAlert        => ProactiveChatMessageType.HealthAlert,
                AiDecisionType.MedicationReminder => ProactiveChatMessageType.MedicationReminder,
                AiDecisionType.WeatherWarning     => ProactiveChatMessageType.WeatherAlert,
                AiDecisionType.AqiWarning         => ProactiveChatMessageType.AQIAlert,
                AiDecisionType.FollowUpCheck      => ProactiveChatMessageType.HealthAlert,
                AiDecisionType.PredictiveRiskAlert => ProactiveChatMessageType.HealthAlert,
                _                                 => ProactiveChatMessageType.ProactiveRecommendation,
            };

            var chatMsgId = await chatSync.SaveProactiveMessageAsync(
                familyId, decision.Message, chatMessageType,
                correlationId: chainId,
                metadata: decision.Metadata,
                ct: ct);

            if (decision.RelatedSessionId.HasValue &&
                decision.Type == AiDecisionType.FollowUpCheck &&
                decision.FollowUp is not null)
            {
                var nextFollowUp = DateTimeOffset.UtcNow.AddMinutes(decision.FollowUp.InMinutes);
                await monitoringSvc.MarkFollowUpSentAsync(decision.RelatedSessionId.Value, nextFollowUp, ct);

                await PublishSseAsync(sse, memberIds, "followup_chain_updated", new
                {
                    familyId,
                    sessionId      = decision.RelatedSessionId,
                    nextFollowUpAt = nextFollowUp,
                    question       = decision.FollowUp.Question,
                    state          = FollowUpChainState.Delivered.ToString(),
                    chainId,
                }, ct);
            }

            await priorityEngine.RecordDecisionAsync(
                familyId, decision.Type, decision.Message, decision.Priority,
                decision.TargetChildId, decision.RelatedSessionId,
                chainId,
                idempotencyKey: $"cca:{chainId}:{decision.Type}",
                contextSnapshot: null,
                escalationRationale: escalationRationale,
                metadata: decision.Metadata is not null
                    ? JsonSerializer.Serialize(decision.Metadata) : null,
                executed: true, ct);

            await PublishSseAsync(sse, memberIds, "ai_decision_created", new
            {
                decision = new
                {
                    id              = 0,    // ephemeral — client uses chainId for grouping
                    familyId,
                    decisionType    = decision.Type.ToString(),
                    message         = decision.Message,
                    priority        = decision.Priority.ToString(),
                    relatedSessionId = decision.RelatedSessionId,
                    targetChildId   = decision.TargetChildId,
                    decisionChainId = chainId,
                    executed        = true,
                    createdAt       = DateTimeOffset.UtcNow,
                },
                chatMessageId = chatMsgId,
            }, ct);

            logger.LogInformation(
                "AI decision executed: chain={ChainId} family={FamilyId} type={Type} priority={Priority}",
                chainId, familyId, decision.Type, decision.Priority);
        }

        if (sessions.Count > 0)
        {
            await memorySvc.RecordObservationAsync(
                familyId,
                "orchestration:lastCycle",
                $"sessions={sessions.Count}, decisions={result.Decisions.Count}, chainId={chainId}",
                AIMemoryType.BehaviorPattern, ct);

            await PublishSseAsync(sse, memberIds, "ai_memory_updated", new
            {
                familyId, chainId, memoriesUpdated = true, sessionCount = sessions.Count,
            }, ct);
        }

        await PublishSseAsync(sse, memberIds, "family_context_updated", new
        {
            familyId,
            activeSessionCount = sessions.Count(s => !s.IsResolved),
            highestSeverity    = sessions.Count > 0 ? (int)sessions.Max(s => s.Severity) : 0,
            hasEmergency       = sessions.Any(s => s.Severity >= MonitoringSeverity.Critical),
            chainId,
            asOf               = DateTimeOffset.UtcNow,
        }, ct);

        logger.LogDebug(
            "Orchestration complete: family={FamilyId} chain={ChainId} sessions={Sessions} decisions={Decisions}",
            familyId, chainId, sessions.Count, result.Decisions.Count);
    }

    private static MonitoringLifecyclePhase DetermineLifecyclePhase(
        Domain.Entities.HealthMonitoringSession session, PredictedRisk prediction)
    {
        if (session.IsResolved) return MonitoringLifecyclePhase.Resolved;

        if (prediction.EscalationProbability > 0.80 || session.Severity >= MonitoringSeverity.Emergency)
            return MonitoringLifecyclePhase.Critical;

        if (prediction.EscalationProbability > 0.55 || session.Severity >= MonitoringSeverity.High)
            return MonitoringLifecyclePhase.Escalated;

        if (session.FollowUpCount > 0 && prediction.EscalationProbability < 0.25)
            return MonitoringLifecyclePhase.Stabilizing;

        if (session.FollowUpCount > 0)
            return MonitoringLifecyclePhase.Monitoring;

        return MonitoringLifecyclePhase.Detected;
    }

    private static async Task PublishSseAsync(
        ISseConnectionManager sse,
        IReadOnlyList<string> memberIds,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        var json     = JsonSerializer.Serialize(payload);
        var sseEvent = new SseEvent(eventType, json);
        foreach (var uid in memberIds)
        {
            try { await sse.PublishAsync(uid, sseEvent, ct); }
            catch { /* SSE failure must never abort the orchestration loop */ }
        }
    }
}
