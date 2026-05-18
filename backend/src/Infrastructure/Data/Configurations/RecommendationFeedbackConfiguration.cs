using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class RecommendationFeedbackConfiguration : IEntityTypeConfiguration<RecommendationFeedback>
{
    public void Configure(EntityTypeBuilder<RecommendationFeedback> builder)
    {
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.FeedbackType).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.RecommendationId);
        builder.HasIndex(e => new { e.RecommendationId, e.UserId }).IsUnique();

        builder.HasOne(e => e.Recommendation)
            .WithMany(r => r.Feedbacks)
            .HasForeignKey(e => e.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
