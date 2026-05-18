using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Data;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class AiTestModeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AiTestModeService _testMode;
    private readonly ILogger<AiTestModeWorker> _logger;

    public AiTestModeWorker(
        IServiceScopeFactory scopeFactory,
        AiTestModeService testMode,
        ILogger<AiTestModeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _testMode     = testMode;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TestMode] Worker started — idle until enabled");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_testMode.IsEnabled)
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            try
            {
                await RunRealCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TestMode] Cycle failed");
            }

            try { await Task.Delay(_testMode.IntervalSeconds * 1000, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("[TestMode] Worker stopping");
    }

    private async Task RunRealCycleAsync(CancellationToken ct)
    {
        using var scope   = _scopeFactory.CreateScope();
        var services      = scope.ServiceProvider;

        var db            = services.GetRequiredService<ApplicationDbContext>();
        var contextSvc    = services.GetRequiredService<IProactiveContextService>();
        var decisionEngine = services.GetRequiredService<IAiDecisionEngine>();
        var monitoringSvc = services.GetRequiredService<IHealthMonitoringService>();
        var voiceSvc      = services.GetRequiredService<IVoiceActionService>();
        var chatSync      = services.GetRequiredService<IChatSynchronizationService>();
        var sse           = services.GetRequiredService<ISseConnectionManager>();

        var familyIds = await db.Families.Select(f => f.Id).ToListAsync(ct);
        if (familyIds.Count == 0) return;

        foreach (var familyId in familyIds)
        {
            try { await RunFamilyAsync(familyId, db, contextSvc, decisionEngine, monitoringSvc, voiceSvc, chatSync, sse, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "[TestMode] Family {FamilyId} failed", familyId); }
        }
    }

    private async Task RunFamilyAsync(
        int familyId,
        ApplicationDbContext db,
        IProactiveContextService contextSvc,
        IAiDecisionEngine decisionEngine,
        IHealthMonitoringService monitoringSvc,
        IVoiceActionService voiceSvc,
        IChatSynchronizationService chatSync,
        ISseConnectionManager sse,
        CancellationToken ct)
    {
        var memberIds = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (memberIds.Count == 0) return;

        var context  = await contextSvc.BuildContextAsync(familyId, ct);
        var sessions = await monitoringSvc.GetActiveSessionsForFamilyAsync(familyId, ct);

        var result  = await decisionEngine.AnalyzeAsync(context, sessions, ct);
        var chainId = result.DecisionChainId ?? Guid.NewGuid().ToString("N")[..12];

        _testMode.AddReasoning(
            $"family={familyId} chain={chainId} decisions={result.Decisions.Count} sessions={sessions.Count}");

        var pushSent = 0;
        var sseSent  = 0;

        var executedThisCycle = new HashSet<string>();

        foreach (var decision in result.Decisions)
        {
            var cycleKey = $"{decision.Type}:{decision.TargetChildId}";
            if (!executedThisCycle.Add(cycleKey)) continue;

            foreach (var userId in memberIds)
            {
                await voiceSvc.CreateAsync(
                    familyId, userId, decision.ActionType,
                    decision.Message, decision.Priority,
                    requiresConfirmation: decision.Priority >= NotificationPriority.Emergency,
                    escalationEnabled:   decision.Priority >= NotificationPriority.High,
                    scheduledAt: null,
                    metadata: JsonSerializer.Serialize(new
                    {
                        source   = "ai_test_mode",
                        chainId,
                        testMode = true,
                    }),
                    idempotencyKey: null,
                    ct);

                _testMode.IncrementVoice();
                pushSent++;
            }

            await chatSync.SaveProactiveMessageAsync(
                familyId, decision.Message,
                decision.Type switch
                {
                    AiDecisionType.EscalationAlert    => ProactiveChatMessageType.EscalationAlert,
                    AiDecisionType.MedicationReminder => ProactiveChatMessageType.MedicationReminder,
                    AiDecisionType.AqiWarning         => ProactiveChatMessageType.AQIAlert,
                    AiDecisionType.WeatherWarning     => ProactiveChatMessageType.WeatherAlert,
                    _                                 => ProactiveChatMessageType.HealthAlert,
                },
                correlationId: chainId,
                ct: ct);

            await PublishSseAsync(sse, memberIds, "ai_decision_created", new
            {
                decision = new
                {
                    familyId,
                    decisionType    = decision.Type.ToString(),
                    message         = decision.Message,
                    priority        = decision.Priority.ToString(),
                    decisionChainId = chainId,
                    executed        = true,
                    createdAt       = DateTimeOffset.UtcNow,
                    testMode        = true,
                },
            }, ct);

            sseSent += memberIds.Count;
            _testMode.IncrementSse(memberIds.Count);
        }

        _testMode.IncrementPush(pushSent);

        _testMode.AddEvent(new TestModeEvent(
            DateTimeOffset.UtcNow,
            familyId,
            chainId,
            result.Decisions.Count,
            pushSent,
            sseSent,
            $"sessions={sessions.Count} decisions={result.Decisions.Count} children={context.Children.Count}"));

        _logger.LogInformation(
            "[TestMode] family={FamilyId} chain={Chain} decisions={Decisions} push={Push}",
            familyId, chainId, result.Decisions.Count, pushSent);
    }

    private static async Task PublishSseAsync(
        ISseConnectionManager sse,
        IReadOnlyList<string> memberIds,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var evt  = new SseEvent(eventType, json);
        foreach (var uid in memberIds)
        {
            try { await sse.PublishAsync(uid, evt, ct); }
            catch { /* SSE failure must never abort the test loop */ }
        }
    }
}
