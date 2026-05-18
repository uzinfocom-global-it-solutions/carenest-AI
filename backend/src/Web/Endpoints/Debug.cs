using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Identity;
using Backend.Infrastructure.Services;
using Backend.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Backend.Web.Endpoints;

public class Debug : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/debug";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Password reset — no auth required (dev only, checked inside handler)
        groupBuilder.MapPost(ResetPassword, "reset-password").AllowAnonymous();

        // Debug endpoints are auth-protected but not rate-limited
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(PingMe, "ping-me");
        groupBuilder.MapPost(TestPush, "push/test");
        groupBuilder.MapPost(TestVoice, "voice/test");
        groupBuilder.MapPost(TriggerMedication, "triggers/medication");
        groupBuilder.MapPost(TestEscalation, "escalation/test");
        groupBuilder.MapPost(TestNotification, "notifications/test");
        groupBuilder.MapGet(GetVoiceQueue, "voice/queue");
        groupBuilder.MapGet(GetDeliveryStatus, "delivery");
        groupBuilder.MapGet(GetEscalationState, "escalation/state");
        groupBuilder.MapGet(GetSseConnections, "sse/connections");

        // AI Test Mode — stress-test the full production pipeline
        groupBuilder.MapPost(StartTestMode,  "ai/test-mode/start");
        groupBuilder.MapPost(StopTestMode,   "ai/test-mode/stop");
        groupBuilder.MapGet(GetTestModeStatus, "ai/test-mode/status");

        // Weather observability
        groupBuilder.MapGet(GetWeatherStatus, "weather/status");

        // AI Operating System — observability
        groupBuilder.MapGet(GetAiDecisionLog, "ai/decisions");
        groupBuilder.MapGet(GetMonitoringDashboard, "ai/monitoring");
        groupBuilder.MapGet(GetAiMemory, "ai/memory");
        groupBuilder.MapGet(GetFollowUpChain, "ai/sessions/{sessionId}/followup-chain");
        groupBuilder.MapPost(TriggerOrchestrationCycle, "ai/orchestrate");
        groupBuilder.MapPost(TriggerMonitoringSession, "ai/monitoring/start");
    }

    [EndpointSummary("Debug: Ping Me — instant push + voice to current user")]
    [EndpointDescription("Sends a push notification + voice action immediately to the authenticated user via SSE.")]
    public static async Task<IResult> PingMe(
        IApplicationDbContext db,
        IPushNotificationService push,
        IVoiceActionService voiceService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        const string text = "Привет! Система CareNestAI работает. Это тестовое голосовое уведомление.";

        // Push via SSE
        var delivered = await push.SendToUserAsync(
            userId, "🔔 CareNestAI — Тест", text, NotificationPriority.High,
            new Dictionary<string, string>
            {
                ["type"] = "voice_action",
                ["actionId"] = "0",
                ["text"] = text,
                ["requiresConfirmation"] = "false",
                ["priority"] = "high",
            }, ct);

        // VoiceAction (triggers TTS on Flutter)
        Domain.Entities.VoiceAction? action = null;
        if (familyId > 0)
        {
            action = await voiceService.CreateAsync(
                familyId, userId,
                Domain.Enums.VoiceActionType.Custom, text,
                NotificationPriority.High,
                requiresConfirmation: false,
                escalationEnabled: false,
                scheduledAt: null, metadata: null,
                idempotencyKey: null, ct);
        }

        return Results.Ok(new
        {
            sseDelivered = delivered,
            voiceActionId = action?.Id,
            message = delivered > 0
                ? "✅ Push отправлен через SSE! Проверь приложение."
                : "⚠️ SSE соединение не активно. Открой Flutter-приложение и попробуй снова.",
        });
    }

    [EndpointSummary("Debug: Test Push Notification")]
    public static async Task<IResult> TestPush(
        TestPushRequest req,
        IPushNotificationService push,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var targetUserId = string.IsNullOrEmpty(req.TargetUserId) ? userId : req.TargetUserId;

        var count = await push.SendToUserAsync(
            targetUserId,
            req.Title ?? "Test Push",
            req.Body ?? "This is a test push from debug panel",
            req.Priority,
            new Dictionary<string, string> { ["type"] = "debug_test" },
            ct);

        return Results.Ok(new { delivered = count, targetUserId });
    }

    [EndpointSummary("Debug: Test Voice Action")]
    public static async Task<IResult> TestVoice(
        TestVoiceRequest req,
        IApplicationDbContext db,
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var action = await service.CreateAsync(
            familyId, userId,
            VoiceActionType.Custom,
            req.Text ?? "Test voice action from debug panel",
            req.Priority,
            req.RequiresConfirmation,
            req.EscalationEnabled,
            scheduledAt: null,
            JsonSerializer.Serialize(new { source = "debug" }),
            idempotencyKey: null, // Debug actions bypass deduplication
            ct);

        if (action is null) return Results.Ok(new { deduplicated = true, userId, familyId });
        return Results.Ok(new { actionId = action.Id, userId, familyId });
    }

    [EndpointSummary("Debug: Trigger Medication Reminder")]
    public static async Task<IResult> TriggerMedication(
        TriggerMedicationRequest req,
        IApplicationDbContext db,
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;

        var schedule = await db.MedicationSchedules
            .Include(s => s.Child)
            .ThenInclude(c => c.Family)
            .ThenInclude(f => f.Members.Where(m => m.Status == FamilyMemberStatusEnum.Active))
            .FirstOrDefaultAsync(s => s.Id == req.ScheduleId, ct);

        if (schedule is null) return Results.NotFound("Medication schedule not found");

        var familyId = schedule.Child.FamilyId;
        var members = schedule.Child.Family.Members;

        var actions = new List<object>();
        foreach (var member in members)
        {
            var action = await service.CreateAsync(
                familyId, member.UserId,
                VoiceActionType.MedicationReminder,
                $"[DEBUG] Время принять {schedule.MedicationName}, доза: {schedule.Dosage}",
                NotificationPriority.High,
                schedule.RequiresConfirmation,
                schedule.EscalationEnabled,
                scheduledAt: null,
                JsonSerializer.Serialize(new { medicationScheduleId = schedule.Id, source = "debug" }),
                idempotencyKey: null, // Debug bypasses dedup
                ct);

            if (action is not null)
                actions.Add(new { actionId = action.Id, userId = member.UserId });
        }

        return Results.Ok(new { triggered = actions.Count, actions });
    }

    [EndpointSummary("Debug: Test Escalation")]
    public static async Task<IResult> TestEscalation(
        TestEscalationRequest req,
        IApplicationDbContext db,
        IPushNotificationService push,
        ISseConnectionManager sse,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;

        var action = await db.VoiceActions
            .FirstOrDefaultAsync(a => a.Id == req.VoiceActionId, ct);

        if (action is null) return Results.NotFound("VoiceAction not found");

        // Force-move delivered time to trigger escalation
        action.DeliveredAt = DateTimeOffset.UtcNow.AddMinutes(-61);
        action.Status = VoiceActionStatus.Delivered;
        action.EscalationStep = 0;
        await db.SaveChangesAsync(ct);

        await sse.PublishAsync(action.UserId, new SseEvent(
            "escalation_test",
            JsonSerializer.Serialize(new { actionId = action.Id, message = "Escalation test initiated" })), ct);

        return Results.Ok(new { message = "Escalation test initiated - engine will process within 2 minutes", actionId = action.Id });
    }

    [EndpointSummary("Debug: Test Notification")]
    public static async Task<IResult> TestNotification(
        TestNotificationRequest req,
        ISseConnectionManager sse,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;

        await sse.PublishAsync(userId, new SseEvent(
            req.EventType ?? "debug_notification",
            JsonSerializer.Serialize(new
            {
                message = req.Message ?? "Test notification",
                timestamp = DateTimeOffset.UtcNow,
                source = "debug",
            })), ct);

        return Results.Ok(new { sent = true, userId, eventType = req.EventType ?? "debug_notification" });
    }

    [EndpointSummary("Debug: Get Voice Action Queue")]
    public static async Task<IResult> GetVoiceQueue(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var actions = await db.VoiceActions
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new
            {
                a.Id, a.Type, a.Priority, a.Status,
                a.Text, a.RequiresConfirmation, a.EscalationEnabled,
                a.EscalationStep, a.DeliveredAt, a.ConfirmedAt, a.CreatedAt
            })
            .ToListAsync(ct);

        return Results.Ok(actions);
    }

    [EndpointSummary("Debug: Get Delivery Status")]
    public static async Task<IResult> GetDeliveryStatus(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var deliveries = await db.NotificationDeliveries
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Id)
            .Take(50)
            .Select(d => new
            {
                d.Id, d.RecommendationId, d.DeliveryChannel,
                d.DeliveryStatus, d.DeliveredAt, d.OpenedAt,
                d.FailedReason, d.RetryCount
            })
            .ToListAsync(ct);

        return Results.Ok(deliveries);
    }

    [EndpointSummary("Debug: Get Escalation State")]
    public static async Task<IResult> GetEscalationState(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var escalations = await db.VoiceActions
            .Where(a => a.UserId == userId && a.EscalationEnabled && a.EscalationStep > 0)
            .OrderByDescending(a => a.LastEscalatedAt)
            .Take(30)
            .Select(a => new
            {
                a.Id, a.Type, a.Text, a.Status,
                a.EscalationStep, a.LastEscalatedAt,
                a.EscalationTargetUserId, a.DeliveredAt, a.ConfirmedAt
            })
            .ToListAsync(ct);

        return Results.Ok(escalations);
    }

    [EndpointSummary("Debug: Weather Status")]
    [EndpointDescription("Returns the last 10 weather snapshots for the DB plus the current family location and SSE connection count. Use to diagnose stale weather or missing API key.")]
    public static async Task<IResult> GetWeatherStatus(
        IApplicationDbContext db,
        IWeatherAdapter weather,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;

        var family = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => new { m.FamilyId, m.Family.DefaultLocationKey, m.Family.DefaultLatitude, m.Family.DefaultLongitude })
            .FirstOrDefaultAsync(ct);

        var recentSnapshots = await db.WeatherSnapshots
            .OrderByDescending(w => w.CollectedAt)
            .Take(10)
            .Select(w => new
            {
                w.Id, w.LocationKey, w.Latitude, w.Longitude,
                w.TemperatureC, w.WeatherCondition, w.Aqi, w.UvIndex,
                w.CollectedAt,
                ageMinutes = (int)(DateTimeOffset.UtcNow - w.CollectedAt).TotalMinutes,
            })
            .ToListAsync(ct);

        var familyLatest = family?.DefaultLocationKey is not null
            ? await weather.GetLatestAsync(family.DefaultLocationKey, ct)
            : null;

        return Results.Ok(new
        {
            userId,
            familyId = family?.FamilyId,
            familyLocation = new
            {
                key = family?.DefaultLocationKey,
                lat = family?.DefaultLatitude,
                lon = family?.DefaultLongitude,
            },
            familyLatestSnapshot = familyLatest is null ? null : new
            {
                familyLatest.LocationKey,
                familyLatest.TemperatureC,
                familyLatest.WeatherCondition,
                familyLatest.CollectedAt,
                ageMinutes = (int)(DateTimeOffset.UtcNow - familyLatest.CollectedAt).TotalMinutes,
            },
            recentSnapshots,
            hint = recentSnapshots.Count == 0
                ? "⚠️ No weather snapshots in DB — check Weather:ApiKey in appsettings.json"
                : recentSnapshots.All(s => s.TemperatureC == 20)
                    ? "⚠️ All snapshots show 20°C fallback — Weather:ApiKey may be invalid"
                    : "✅ Weather data appears valid",
        });
    }

    [EndpointSummary("Debug: Get SSE Connections")]
    public static IResult GetSseConnections(
        ISseConnectionManager sse,
        HttpContext ctx)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var count = sse.GetConnectionCount(userId);
        return Results.Ok(new { userId, activeConnections = count });
    }

    [EndpointSummary("Debug: Reset Password (Dev Only)")]
    public static async Task<IResult> ResetPassword(
        ResetPasswordRequest req,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment env)
    {
        if (!env.IsDevelopment())
            return Results.Forbid();

        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null)
            return Results.NotFound(new { error = $"User '{req.Email}' not found" });

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, req.NewPassword);

        if (!result.Succeeded)
            return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Results.Ok(new { message = $"Password reset for {req.Email}", email = req.Email });
    }

    [EndpointSummary("Debug: AI Decision Log Inspector")]
    [EndpointDescription("Returns recent AI decisions for the current user's family, ordered by creation time descending.")]
    public static async Task<IResult> GetAiDecisionLog(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct,
        int limit = 50)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var logs = await db.AiDecisionLogs
            .Where(d => d.FamilyId == familyId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(d => new
            {
                d.Id, d.DecisionType, d.Message, d.Priority,
                d.TargetChildId, d.RelatedSessionId, d.DecisionChainId,
                d.Executed, d.SuppressedBy, d.EscalationRationale, d.CreatedAt,
            })
            .ToListAsync(ct);

        return Results.Ok(new { familyId, count = logs.Count, logs });
    }

    [EndpointSummary("Debug: Monitoring Dashboard")]
    [EndpointDescription("Returns all active monitoring sessions with lifecycle, predicted risk, and follow-up state.")]
    public static async Task<IResult> GetMonitoringDashboard(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var sessions = await db.HealthMonitoringSessions
            .Where(s => s.FamilyId == familyId)
            .OrderByDescending(s => s.StartedAt)
            .Take(30)
            .Select(s => new
            {
                s.Id, s.ChildId, s.IssueType, s.Severity, s.Status,
                s.LifecyclePhase, s.RiskScore, s.PredictedRiskScore,
                s.PredictedEscalationAt, s.EscalationLevel,
                s.FollowUpCount, s.MissedFollowUps,
                s.NextFollowUpAt, s.IsResolved,
                s.MonitoringChainId, s.StartedAt, s.LastUpdatedAt,
                s.InitialSymptomDescription,
            })
            .ToListAsync(ct);

        var activeSessions = sessions.Count(s => !s.IsResolved);
        var hasEmergency   = sessions.Any(s => !s.IsResolved && (int)s.Severity >= 4);

        return Results.Ok(new
        {
            familyId,
            activeSessions,
            hasEmergency,
            sessions,
        });
    }

    [EndpointSummary("Debug: AI Memory Explorer")]
    public static async Task<IResult> GetAiMemory(
        IApplicationDbContext db,
        IAIMemoryService memorySvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var memories = await memorySvc.GetFamilyMemoryAsync(familyId, ct);
        var summary  = await memorySvc.BuildMemoryContextAsync(familyId, ct);

        return Results.Ok(new
        {
            familyId,
            count = memories.Count,
            summary,
            memories = memories.Select(m => new
            {
                m.Id, m.Key, m.Value, m.MemoryType,
                m.RelevanceScore, m.ObservationCount,
                m.LastObservedAt, m.ExpiresAt,
            }),
        });
    }

    [EndpointSummary("Debug: Follow-Up Chain for Session")]
    public static async Task<IResult> GetFollowUpChain(
        int sessionId,
        IFollowUpIntelligenceService followUpSvc,
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var session = await db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return Results.NotFound();

        var chain = await followUpSvc.GetFollowUpChainAsync(sessionId, ct);

        return Results.Ok(new
        {
            sessionId,
            issueType = session.IssueType,
            severity  = session.Severity,
            lifecyclePhase = session.LifecyclePhase,
            followUpCount  = session.FollowUpCount,
            missedFollowUps = session.MissedFollowUps,
            nextFollowUpAt = session.NextFollowUpAt,
            responseAnalysis = session.ResponseAnalysisJson,
            monitoringChainId = session.MonitoringChainId,
            predictedRiskScore = session.PredictedRiskScore,
            predictedEscalationAt = session.PredictedEscalationAt,
            chain,
        });
    }

    [EndpointSummary("Debug: Trigger Orchestration Cycle")]
    [EndpointDescription("Manually triggers one AI orchestration cycle for the current user's family. Use to test the autonomous loop.")]
    public static async Task<IResult> TriggerOrchestrationCycle(
        IApplicationDbContext db,
        IProactiveContextService contextSvc,
        IAiDecisionEngine decisionEngine,
        IHealthMonitoringService monitoringSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var context  = await contextSvc.BuildContextAsync(familyId, ct);
        var sessions = await monitoringSvc.GetActiveSessionsForFamilyAsync(familyId, ct);
        var result   = await decisionEngine.AnalyzeAsync(context, sessions, ct);

        return Results.Ok(new
        {
            familyId,
            chainId     = result.DecisionChainId,
            decisions   = result.Decisions.Count,
            updates     = result.SessionUpdates.Count,
            predictions = result.PredictedRisks?.Count ?? 0,
            decisionList = result.Decisions.Select(d => new
            {
                d.Type, d.Message, d.Priority,
                d.TargetChildId, d.RelatedSessionId,
            }),
            predictedRisks = result.PredictedRisks?.Select(p => new
            {
                p.EscalationProbability, p.PredictedSeverity,
                p.TimeToEscalationMinutes, p.PredictedRiskScore,
                p.RiskFactors,
            }),
        });
    }

    [EndpointSummary("Debug: Manually Start Monitoring Session")]
    public static async Task<IResult> TriggerMonitoringSession(
        StartMonitoringRequest req,
        IApplicationDbContext db,
        IHealthMonitoringService monitoringSvc,
        IHealthRiskScoringService riskScoring,
        IProactiveContextService contextSvc,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        if (!Enum.TryParse<MonitoringIssueType>(req.IssueType, ignoreCase: true, out var issueType))
            return Results.BadRequest($"Unknown issue type '{req.IssueType}'");

        var context = await contextSvc.BuildContextAsync(familyId, ct);
        var child = req.ChildId.HasValue
            ? context.Children.FirstOrDefault(c => c.ChildId == req.ChildId.Value)
            : null;

        var risk = riskScoring.CalculateRisk(issueType, child, context.Weather, req.Description, 0, 0);
        var session = await monitoringSvc.StartOrUpdateSessionAsync(
            familyId, req.ChildId, issueType, req.Description ?? "[Debug trigger]", risk, ct);

        return Results.Ok(new
        {
            sessionId = session.Id,
            familyId,
            issueType = session.IssueType.ToString(),
            severity = session.Severity.ToString(),
            riskScore = session.RiskScore,
            lifecyclePhase = session.LifecyclePhase.ToString(),
            monitoringChainId = session.MonitoringChainId,
            nextFollowUpAt = session.NextFollowUpAt,
        });
    }

    [EndpointSummary("Debug: Start AI Test Mode")]
    [EndpointDescription("Enables the 5-second AI stress-test loop. Every tick injects a synthetic monitoring scenario and runs it through the full production pipeline: context → risk → decisions → priority dedup → voice → SSE → memory.")]
    public static IResult StartTestMode(
        StartTestModeRequest req,
        AiTestModeService testMode)
    {
        testMode.Enable(req.IntervalSeconds);
        return Results.Ok(new
        {
            enabled         = true,
            intervalSeconds = testMode.IntervalSeconds,
            message         = $"✅ AI Test Mode ACTIVE — firing every {testMode.IntervalSeconds}s through full production pipeline",
        });
    }

    [EndpointSummary("Debug: Stop AI Test Mode")]
    public static IResult StopTestMode(AiTestModeService testMode)
    {
        var stats = testMode.GetStats();
        testMode.Disable();
        return Results.Ok(new
        {
            enabled    = false,
            message    = "⏹ AI Test Mode stopped",
            finalStats = stats,
        });
    }

    [EndpointSummary("Debug: Get AI Test Mode Status & Live Stats")]
    public static IResult GetTestModeStatus(AiTestModeService testMode) =>
        Results.Ok(testMode.GetStats());
}

public record ResetPasswordRequest(string Email, string NewPassword);
public record TestPushRequest(string? TargetUserId, string? Title, string? Body, NotificationPriority Priority = NotificationPriority.Normal);
public record TestVoiceRequest(string? Text, NotificationPriority Priority = NotificationPriority.Normal, bool RequiresConfirmation = false, bool EscalationEnabled = false);
public record TriggerMedicationRequest(int ScheduleId);
public record TestEscalationRequest(int VoiceActionId);
public record TestNotificationRequest(string? EventType, string? Message);
public record StartMonitoringRequest(string IssueType, string? Description, int? ChildId);
public record StartTestModeRequest(int? IntervalSeconds);
