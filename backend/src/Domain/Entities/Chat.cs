using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Chat : BaseEntity
{
    public required int FamilyId { get; set; }
    public required string CreatedByUserId { get; set; }
    public ChatTypeEnum ChatType { get; set; } = ChatTypeEnum.Family;
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
