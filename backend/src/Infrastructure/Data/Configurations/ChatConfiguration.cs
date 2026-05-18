using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.ChatType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Title).HasMaxLength(200);

        builder.HasIndex(e => e.FamilyId);

        builder.HasOne(e => e.Family)
            .WithMany(f => f.Chats)
            .HasForeignKey(e => e.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
