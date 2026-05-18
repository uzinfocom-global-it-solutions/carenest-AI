using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.DeliveryChannel).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.DeliveryStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.FailedReason).HasMaxLength(500);

        builder.HasIndex(e => e.RecommendationId);
        builder.HasIndex(e => new { e.UserId, e.DeliveryStatus });

        builder.HasOne(e => e.Recommendation)
            .WithMany(r => r.Deliveries)
            .HasForeignKey(e => e.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
