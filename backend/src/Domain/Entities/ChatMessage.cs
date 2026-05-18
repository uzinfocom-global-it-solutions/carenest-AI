using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public required int ChatId { get; set; }
    public string? UserId { get; set; }
    public required SenderTypeEnum SenderType { get; set; }
    public required MessageTypeEnum MessageType { get; set; }
    public InputModeEnum InputMode { get; set; } = InputModeEnum.Text;
    public required string Content { get; set; }
    public string? ParsedIntent { get; set; }
    public string? AiResponse { get; set; }
    public bool Successful { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Chat Chat { get; set; } = null!;
}
