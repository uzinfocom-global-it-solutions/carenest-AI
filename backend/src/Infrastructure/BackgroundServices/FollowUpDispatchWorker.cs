using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

/// Runs every 30 seconds. Dispatches follow-up questions whose scheduled time has arrived.
/// Only dispatches VoiceActions already created by the orchestration pipeline —
/// does NOT make new AI decisions. Updates session state and emits SSE after dispatch.
internal sealed class FollowUpDispatchWorker : PeriodicBackgroundService
{
    public FollowUpDispatchWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<FollowUpDispatchWorker> logger)
        : base(scopeFactory, logger) { }

    protected override string ServiceName => nameof(FollowUpDispatchWorker);
    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteIterationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db          = services.GetRequiredService<IApplicationDbContext>();
        var chatSync    = services.GetRequiredService<IChatSynchronizationService>();
        var monSvc      = services.GetRequiredService<IHealthMonitoringService>();
        var sse         = services.GetRequiredService<ISseConnectionManager>();
        var logger      = services.GetRequiredService<ILogger<FollowUpDispatchWorker>>();

        var now = DateTimeOffset.UtcNow;

        var due = await db.VoiceActions
            .Where(a => a.Type       == VoiceActionType.FollowUpCheck
                     && a.Status     == VoiceActionStatus.Pending
                     && a.ScheduledAt != null
                     && a.ScheduledAt <= now)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        // Group by family — one chat message per family per dispatch cycle
        var byFamily = due.GroupBy(a => a.FamilyId);

        foreach (var group in byFamily)
        {
            var familyId = group.Key;

            // Pick the highest-priority action as the dispatched question
            var lead = group
                .OrderByDescending(a => (int)a.Priority)
                .First();

            var correlationId = $"followup:{familyId}:{now:yyyyMMddHHmm}";

            // Dispatch the follow-up question to chat
            try
            {
                await chatSync.SaveProactiveMessageAsync(
                    familyId,
                    lead.Text,
                    ProactiveChatMessageType.HealthAlert,
                    correlationId: correlationId,
                    metadata: new Dictionary<string, string>
                    {
                        ["type"]    = "follow_up",
                        ["chainId"] = correlationId,
                    },
                    ct: ct);

                logger.LogInformation(
                    "Follow-up dispatched for family {FamilyId}: {Question}",
                    familyId, lead.Text.Length > 60 ? lead.Text[..60] + "…" : lead.Text);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Follow-up dispatch failed for family {FamilyId}", familyId);
                continue;
            }

            // Mark all dispatched actions as Delivered
            foreach (var action in group)
                action.Status = VoiceActionStatus.Delivered;

            // Update monitoring session follow-up state and emit SSE
            // Extract relatedSessionId from metadata if available
            foreach (var action in group)
            {
                if (action.Metadata is null) continue;
                try
                {
                    using var doc = JsonDocument.Parse(action.Metadata);
                    if (!doc.RootElement.TryGetProperty("sessionId", out var sidEl)) continue;
                    var sessionId = sidEl.GetInt32();

                    var nextAt = now.AddMinutes(30); // conservative default for un-answered follow-ups
                    await monSvc.MarkFollowUpSentAsync(sessionId, nextAt, ct);

                    // Emit SSE for this family's active members
                    var memberIds = await db.FamilyMembers
                        .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
                        .Select(m => m.UserId)
                        .ToListAsync(ct);

                    var ssePayload = JsonSerializer.Serialize(new
                    {
                        familyId,
                        sessionId,
                        nextFollowUpAt = nextAt,
                        question       = action.Text,
                        state          = FollowUpChainState.Delivered.ToString(),
                        correlationId,
                    });

                    foreach (var uid in memberIds)
                    {
                        try
                        {
                            await sse.PublishAsync(
                                uid, new SseEvent("followup_chain_updated", ssePayload), ct);
                        }
                        catch { /* SSE failure must not abort dispatch */ }
                    }

                    // Only process first action per family that has a sessionId
                    break;
                }
                catch { /* Metadata parse failure is non-fatal */ }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
