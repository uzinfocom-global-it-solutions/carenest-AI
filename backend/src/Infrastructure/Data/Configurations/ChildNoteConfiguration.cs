using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChildNoteConfiguration : IEntityTypeConfiguration<ChildNote>
{
    public void Configure(EntityTypeBuilder<ChildNote> builder)
    {
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.NoteType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Note).IsRequired();
        builder.Property(e => e.Tags).HasMaxLength(500);

        builder.HasIndex(e => e.ChildId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.Child)
            .WithMany(c => c.Notes)
            .HasForeignKey(e => e.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
