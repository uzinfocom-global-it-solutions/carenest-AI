using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.HasIndex(e => e.RefreshTokenHash).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.Property(e => e.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.DeviceId).HasMaxLength(256);
    }
}
