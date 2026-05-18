using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.LocationLabel).HasMaxLength(200);
        builder.Property(e => e.LocationKey).HasMaxLength(100);
        builder.Property(e => e.LocationType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ActivityIntensity).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450);

        builder.HasIndex(e => e.FamilyId);
        builder.HasIndex(e => e.ChildId);
        builder.HasIndex(e => e.StartDatetime);

        builder.HasOne(e => e.Family)
            .WithMany(f => f.Events)
            .HasForeignKey(e => e.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
