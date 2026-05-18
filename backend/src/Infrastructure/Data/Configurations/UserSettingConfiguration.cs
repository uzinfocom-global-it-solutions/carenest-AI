using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.PreferredInputMode).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.PreferredOutputMode).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.NotificationLevel).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ProactiveMode).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.PreferredLanguage).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Timezone).HasMaxLength(64).IsRequired();

        builder.HasIndex(e => e.UserId).IsUnique();
    }
}
