using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class RecommendationFeedback : BaseEntity
{
    public required int RecommendationId { get; set; }
    public required string UserId { get; set; }
    public required FeedbackTypeEnum FeedbackType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Recommendation Recommendation { get; set; } = null!;
}
