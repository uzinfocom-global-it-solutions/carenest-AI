using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChildSensitivityConfiguration : IEntityTypeConfiguration<ChildSensitivity>
{
    public void Configure(EntityTypeBuilder<ChildSensitivity> builder)
    {
        builder.HasIndex(e => e.ChildId).IsUnique();
    }
}
