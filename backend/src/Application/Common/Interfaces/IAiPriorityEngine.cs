using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

/// Central deduplication and suppression engine for AI-generated notifications.
/// Prevents notification flooding by enforcing cooldown windows per decision type,
/// priority, family, child, and session.
public interface IAiPriorityEngine
{
    /// Returns whether a decision of the given type should be suppressed
    /// because a similar one was recently executed within the cooldown window.
    Task<PriorityDecision> EvaluateAsync(
        int familyId,
        AiDecisionType decisionType,
        NotificationPriority priority,
        int? childId,
        int? sessionId,
        CancellationToken ct = default);

    /// Persists a decision record. Pass executed=false for suppressed decisions.
    Task RecordDecisionAsync(
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
        CancellationToken ct = default);
}

public record PriorityDecision(
    bool ShouldSuppress,
    string? SuppressReason,
    NotificationPriority EffectivePriority);
