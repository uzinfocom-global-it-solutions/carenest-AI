using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Recommendation : BaseEntity
{
    public required int FamilyId { get; set; }
    public required int ChildId { get; set; }
    public int? CalendarEventId { get; set; }
    public int? WeatherSnapshotId { get; set; }
    public required RecommendationTypeEnum RecommendationType { get; set; }
    public required string Priority { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? Reason { get; set; }
    public DeliveryTypeEnum DeliveryType { get; set; } = DeliveryTypeEnum.InApp;
    public RecommendationStatusEnum Status { get; set; } = RecommendationStatusEnum.Pending;
    public string? GeneratedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public required DateTimeOffset ExpiresAt { get; set; }

    public Child Child { get; set; } = null!;
    public ICollection<RecommendationFeedback> Feedbacks { get; set; } = [];
    public ICollection<NotificationDelivery> Deliveries { get; set; } = [];
}
