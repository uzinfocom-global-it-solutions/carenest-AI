using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// Central deduplication and suppression gate for all AI-generated notifications.
///
/// Suppression windows by priority:
///   Emergency : 2 min  (almost never suppressed — prevents exact duplicate in same cycle)
///   High      : 10 min
///   Normal    : 30 min
///   Low       : 60 min
///
/// Emergency alerts from escalation chains are never suppressed for different sessions.
public sealed class AiPriorityEngine : IAiPriorityEngine
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AiPriorityEngine> _logger;

    private static readonly TimeSpan EmergencyWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HighWindow      = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NormalWindow    = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LowWindow       = TimeSpan.FromMinutes(60);

    public AiPriorityEngine(IApplicationDbContext db, ILogger<AiPriorityEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PriorityDecision> EvaluateAsync(
        int familyId,
        AiDecisionType decisionType,
        NotificationPriority priority,
        int? childId,
        int? sessionId,
        CancellationToken ct = default)
    {
        var window = priority switch
        {
            NotificationPriority.Emergency => EmergencyWindow,
            NotificationPriority.High      => HighWindow,
            NotificationPriority.Normal    => NormalWindow,
            _                              => LowWindow,
        };

        var cutoff = DateTimeOffset.UtcNow - window;

        var recentSimilar = await _db.AiDecisionLogs
            .Where(d =>
                d.FamilyId      == familyId &&
                d.DecisionType  == decisionType.ToString() &&
                d.TargetChildId == childId &&
                d.RelatedSessionId == sessionId &&
                d.Executed &&
                d.SuppressedBy == null &&
                d.CreatedAt >= cutoff)
            .AnyAsync(ct);

        if (recentSimilar)
        {
            var reason =
                $"{decisionType} already delivered within {window.TotalMinutes:0}min window for family {familyId}";

            _logger.LogDebug(
                "Priority engine suppressed {Type} for family {FamilyId} (child={Child}, session={Session})",
                decisionType, familyId, childId, sessionId);

            return new PriorityDecision(true, reason, priority);
        }

        return new PriorityDecision(false, null, priority);
    }

    public async Task RecordDecisionAsync(
        int familyId,
        AiDecisionType decisionType,
        string message,
        NotificationPriority priority,
        int? childId,
        int? sessionId,
        string? decisionChainId,
        string? idempotencyKey,
        string? contextSnapshot,
        string? escalationRationale,
        string? metadata,
        bool executed = true,
        CancellationToken ct = default)
    {
        var log = new AiDecisionLog
        {
            FamilyId            = familyId,
            DecisionType        = decisionType.ToString(),
            Message             = message.Length > 2000 ? message[..2000] : message,
            Priority            = priority.ToString(),
            TargetChildId       = childId,
            RelatedSessionId    = sessionId,
            DecisionChainId     = decisionChainId,
            IdempotencyKey      = idempotencyKey,
            Executed            = executed,
            SuppressedBy        = executed ? null : escalationRationale,
            ContextSnapshotJson = contextSnapshot,
            EscalationRationale = executed ? escalationRationale : null,
            Metadata            = metadata,
            CreatedAt           = DateTimeOffset.UtcNow,
        };

        _db.AiDecisionLogs.Add(log);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Non-fatal — the decision was already executed; log persistence failure is acceptable
            _logger.LogWarning(ex,
                "Failed to persist AiDecisionLog for family {FamilyId}, type={Type}",
                familyId, decisionType);
        }
    }
}
