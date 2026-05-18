using Backend.Domain.Enums;

namespace Backend.Application.Chats.Models;

public record ChatResult(
    int Id,
    int FamilyId,
    string CreatedByUserId,
    ChatTypeEnum ChatType,
    string? Title,
    DateTimeOffset CreatedAt
);

public record ChatMessageResult(
    int Id,
    int ChatId,
    string? UserId,
    SenderTypeEnum SenderType,
    MessageTypeEnum MessageType,
    string Content,
    string? AiResponse,
    string? ParsedIntent,
    bool Successful,
    DateTimeOffset CreatedAt
);
