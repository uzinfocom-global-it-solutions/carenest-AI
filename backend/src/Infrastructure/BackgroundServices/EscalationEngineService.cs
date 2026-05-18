using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

public sealed class EscalationEngineService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EscalationEngineService> _logger;

    private static readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _step1Delay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _step2Delay = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _step3Delay = TimeSpan.FromHours(1);
    private static readonly TimeSpan _perActionCooldown = TimeSpan.FromMinutes(8);

    public EscalationEngineService(IServiceScopeFactory scopeFactory, ILogger<EscalationEngineService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EscalationEngineService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "EscalationEngineService cycle failed");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var sse = scope.ServiceProvider.GetRequiredService<ISseConnectionManager>();

        var now = DateTimeOffset.UtcNow;
        var cooldownCutoff = now - _perActionCooldown;

        var candidates = await db.VoiceActions
            .Include(a => a.Family)
            .ThenInclude(f => f.Members.Where(m => m.Status == FamilyMemberStatusEnum.Active))
            .Where(a =>
                a.EscalationEnabled &&
                a.RequiresConfirmation &&
                a.ConfirmedAt == null &&
                a.DeliveredAt != null &&
                a.EscalationStep < 3 &&
                (a.Status == VoiceActionStatus.Delivered ||
                 a.Status == VoiceActionStatus.Escalated) &&
                (a.LastEscalatedAt == null || a.LastEscalatedAt < cooldownCutoff))
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var changed = 0;
        foreach (var action in candidates)
        {
            var elapsed = now - action.DeliveredAt!.Value;

            var escalated = false;
            if (action.EscalationStep == 0 && elapsed >= _step1Delay)
                escalated = await EscalateStep1Async(action, push, sse, now, ct);
            else if (action.EscalationStep == 1 && elapsed >= _step2Delay)
                escalated = await EscalateStep2Async(action, push, sse, now, ct);
            else if (action.EscalationStep == 2 && elapsed >= _step3Delay)
                escalated = await EscalateStep3Async(action, push, sse, now, ct);

            if (escalated) changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task<bool> EscalateStep1Async(
        Domain.Entities.VoiceAction action,
        IPushNotificationService push,
        ISseConnectionManager sse,
        DateTimeOffset now,
        CancellationToken ct)
    {
        action.EscalationStep = 1;
        action.LastEscalatedAt = now;
        action.Status = VoiceActionStatus.Escalated;

        await push.SendToUserAsync(
            action.UserId,
            "⚠️ Напоминание не подтверждено",
            $"Повторное: {action.Text}",
            NotificationPriority.High,
            new Dictionary<string, string>
            {
                ["type"] = "voice_action",
                ["actionId"] = action.Id.ToString(),
                ["escalationStep"] = "1",
                ["requiresConfirmation"] = "true",
                ["text"] = action.Text,
            }, ct);

        await sse.PublishAsync(action.UserId, new SseEvent(
            "escalation_step1",
            JsonSerializer.Serialize(new { actionId = action.Id, step = 1 })), ct);

        _logger.LogInformation("Escalation step1 for VoiceAction {Id} user {UserId}", action.Id, action.UserId);
        return true;
    }

    private async Task<bool> EscalateStep2Async(
        Domain.Entities.VoiceAction action,
        IPushNotificationService push,
        ISseConnectionManager sse,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var secondary = action.Family.Members.FirstOrDefault(m => m.UserId != action.UserId);
        if (secondary is null)
        {
            action.EscalationStep = 2;
            action.LastEscalatedAt = now;
            return true;
        }

        action.EscalationStep = 2;
        action.LastEscalatedAt = now;
        action.EscalationTargetUserId = secondary.UserId;

        await push.SendToUserAsync(
            secondary.UserId,
            "⚠️ Требуется внимание",
            $"Напоминание члена семьи не подтверждено: {action.Text}",
            NotificationPriority.High,
            new Dictionary<string, string>
            {
                ["type"] = "escalation",
                ["actionId"] = action.Id.ToString(),
                ["escalationStep"] = "2",
                ["originalUserId"] = action.UserId,
            }, ct);

        await sse.PublishAsync(secondary.UserId, new SseEvent(
            "escalation_step2",
            JsonSerializer.Serialize(new
            {
                actionId = action.Id, step = 2,
                originalUserId = action.UserId,
                text = action.Text,
            })), ct);

        _logger.LogInformation("Escalation step2 for VoiceAction {Id} → secondary {Secondary}",
            action.Id, secondary.UserId);
        return true;
    }

    private async Task<bool> EscalateStep3Async(
        Domain.Entities.VoiceAction action,
        IPushNotificationService push,
        ISseConnectionManager sse,
        DateTimeOffset now,
        CancellationToken ct)
    {
        action.EscalationStep = 3;
        action.LastEscalatedAt = now;
        action.Status = VoiceActionStatus.Escalated;

        await push.SendToFamilyAsync(
            action.FamilyId,
            "🚨 ЭКСТРЕННОЕ УВЕДОМЛЕНИЕ",
            $"Критически важное напоминание не подтверждено более часа: {action.Text}",
            NotificationPriority.Emergency,
            new Dictionary<string, string>
            {
                ["type"] = "emergency_escalation",
                ["actionId"] = action.Id.ToString(),
                ["escalationStep"] = "3",
                ["text"] = action.Text,
            }, ct);

        foreach (var member in action.Family.Members)
        {
            await sse.PublishAsync(member.UserId, new SseEvent(
                "emergency_escalation",
                JsonSerializer.Serialize(new { actionId = action.Id, step = 3, text = action.Text })), ct);
        }

        _logger.LogWarning("Escalation step3 EMERGENCY for VoiceAction {Id} family {FamilyId}",
            action.Id, action.FamilyId);
        return true;
    }
}
