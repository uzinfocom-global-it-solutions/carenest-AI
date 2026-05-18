using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.DefaultLocationKey).HasMaxLength(100);

        builder.HasIndex(e => e.CreatedByUserId);
    }
}
