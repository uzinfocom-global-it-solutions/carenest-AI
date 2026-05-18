using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChildConfiguration : IEntityTypeConfiguration<Child>
{
    public void Configure(EntityTypeBuilder<Child> builder)
    {
        builder.Property(e => e.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.AgeGroup).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.FamilyId);

        builder.HasOne(e => e.Family)
            .WithMany(f => f.Children)
            .HasForeignKey(e => e.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Sensitivity)
            .WithOne(s => s.Child)
            .HasForeignKey<ChildSensitivity>(s => s.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
