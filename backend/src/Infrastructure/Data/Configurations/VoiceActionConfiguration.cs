using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class VoiceActionConfiguration : IEntityTypeConfiguration<VoiceAction>
{
    public void Configure(EntityTypeBuilder<VoiceAction> builder)
    {
        builder.ToTable("VoiceActions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.Metadata)
            .HasColumnType("jsonb");

        builder.Property(a => a.EscalationTargetUserId)
            .HasMaxLength(450);

        builder.HasOne(a => a.Family)
            .WithMany()
            .HasForeignKey(a => a.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.UserId, a.Status });
        builder.HasIndex(a => new { a.FamilyId, a.CreatedAt });
        builder.Property(a => a.IdempotencyKey)
            .HasMaxLength(64);

        builder.HasIndex(a => a.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasIndex(a => new { a.Status, a.NextRetryAt });
    }
}
