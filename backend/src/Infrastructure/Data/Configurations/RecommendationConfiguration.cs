using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.Property(e => e.Priority).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Message).IsRequired();
        builder.Property(e => e.GeneratedBy).HasMaxLength(100);
        builder.Property(e => e.RecommendationType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.DeliveryType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.FamilyId);
        builder.HasIndex(e => e.ChildId);
        builder.HasIndex(e => new { e.ChildId, e.Status });
        builder.HasIndex(e => e.ExpiresAt);

        builder.HasOne(e => e.Child)
            .WithMany(c => c.Recommendations)
            .HasForeignKey(e => e.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
