using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChildRoutineConfiguration : IEntityTypeConfiguration<ChildRoutine>
{
    public void Configure(EntityTypeBuilder<ChildRoutine> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RoutineType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.RepeatPattern).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.LocationType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ActivityIntensity).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.ChildId);
        builder.HasIndex(e => new { e.ChildId, e.Active });

        builder.HasOne(e => e.Child)
            .WithMany(c => c.Routines)
            .HasForeignKey(e => e.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
