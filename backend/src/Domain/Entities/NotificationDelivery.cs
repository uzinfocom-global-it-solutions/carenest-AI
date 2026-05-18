using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class NotificationDelivery : BaseEntity
{
    public required int RecommendationId { get; set; }
    public required string UserId { get; set; }
    public required DeliveryChannelEnum DeliveryChannel { get; set; }
    public DeliveryStatusEnum DeliveryStatus { get; set; } = DeliveryStatusEnum.Pending;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public string? FailedReason { get; set; }
    public int RetryCount { get; set; } = 0;

    public Recommendation Recommendation { get; set; } = null!;
}
