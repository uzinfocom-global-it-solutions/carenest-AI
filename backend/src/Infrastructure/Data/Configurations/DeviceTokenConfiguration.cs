using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(t => t.Platform)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.DeviceName)
            .HasMaxLength(200);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => new { t.UserId, t.IsActive });
    }
}
