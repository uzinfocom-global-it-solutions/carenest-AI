using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.Property(e => e.UserId).HasMaxLength(450);
        builder.Property(e => e.SenderType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.MessageType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.InputMode).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Content).IsRequired();

        builder.HasIndex(e => e.ChatId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.Chat)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
