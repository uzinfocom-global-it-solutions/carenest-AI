using Backend.Domain.Enums;

namespace Backend.Application.Recommendations.Models;

public record RecommendationResult(
    int Id,
    int FamilyId,
    int ChildId,
    RecommendationTypeEnum Type,
    string Priority,
    string Title,
    string Message,
    string? Reason,
    RecommendationStatusEnum Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt
);

public record RecommendationFeedbackResult(
    int Id,
    int RecommendationId,
    string UserId,
    FeedbackTypeEnum FeedbackType,
    DateTimeOffset CreatedAt
);
