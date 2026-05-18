using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class WeatherSnapshotConfiguration : IEntityTypeConfiguration<WeatherSnapshot>
{
    public void Configure(EntityTypeBuilder<WeatherSnapshot> builder)
    {
        builder.Property(e => e.LocationKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Aqi).HasMaxLength(50);
        builder.Property(e => e.PollenLevel).HasMaxLength(50);
        builder.Property(e => e.WeatherCondition).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.LocationKey);
        builder.HasIndex(e => new { e.LocationKey, e.ForecastTime });
        builder.HasIndex(e => e.CollectedAt);
    }
}
