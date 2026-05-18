using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IAiPriorityEngine
{
    Task<PriorityDecision> EvaluateAsync(
        int familyId,
        AiDecisionType decisionType,
        NotificationPriority priority,
        int? childId,
        int? sessionId,
        CancellationToken ct = default);

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
