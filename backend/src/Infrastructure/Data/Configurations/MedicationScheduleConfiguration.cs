using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class MedicationScheduleConfiguration : IEntityTypeConfiguration<MedicationSchedule>
{
    public void Configure(EntityTypeBuilder<MedicationSchedule> builder)
    {
        builder.ToTable("MedicationSchedules");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MedicationName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Dosage)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Timezone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.RepeatPattern)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.CustomDays)
            .HasColumnType("jsonb");

        builder.HasOne(m => m.Child)
            .WithMany()
            .HasForeignKey(m => m.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ChildId, m.IsActive });
        builder.HasIndex(m => new { m.IsActive, m.ScheduleTime });
    }
}
