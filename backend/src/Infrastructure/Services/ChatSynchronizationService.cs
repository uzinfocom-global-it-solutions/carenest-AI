using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class ChatSynchronizationService : IChatSynchronizationService
{
    private readonly IApplicationDbContext _db;
    private readonly ISseConnectionManager _sse;
    private readonly ILogger<ChatSynchronizationService> _logger;

    public ChatSynchronizationService(
        IApplicationDbContext db,
        ISseConnectionManager sse,
        ILogger<ChatSynchronizationService> logger)
    {
        _db = db;
        _sse = sse;
        _logger = logger;
    }

    public async Task<int?> SaveProactiveMessageAsync(
        int familyId,
        string text,
        ProactiveChatMessageType messageType,
        string? correlationId = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            // Resolve the MessageTypeEnum based on the proactive message type
            var msgTypeEnum = ResolveMessageType(messageType);

            // Find or create the family's chat
            var chat = await _db.Chats
                .Where(c => c.FamilyId == familyId)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (chat is null)
            {
                // Get the first family member to be the "created by" user for the chat
                var creatorUserId = await _db.FamilyMembers
                    .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
                    .Select(m => m.UserId)
                    .FirstOrDefaultAsync(ct);

                if (creatorUserId is null)
                {
                    _logger.LogWarning("No active members in family {FamilyId} — skipping chat sync", familyId);
                    return null;
                }

                chat = new Chat
                {
                    FamilyId = familyId,
                    CreatedByUserId = creatorUserId,
                    ChatType = ChatTypeEnum.Family,
                    Title = "Семейный чат",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                _db.Chats.Add(chat);
                await _db.SaveChangesAsync(ct);
            }

            // Build ParsedIntent JSON
            var parsedIntent = JsonSerializer.Serialize(new
            {
                eventType = messageType.ToString(),
                correlationId = correlationId,
                isProactive = true,
                metadata = metadata ?? new Dictionary<string, string>(),
            });

            var message = new ChatMessage
            {
                ChatId = chat.Id,
                UserId = null, // system/AI message
                SenderType = SenderTypeEnum.Ai,
                MessageType = msgTypeEnum,
                InputMode = InputModeEnum.Text,
                Content = text,
                ParsedIntent = parsedIntent,
                Successful = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _db.ChatMessages.Add(message);

            // Update chat's UpdatedAt
            chat.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Proactive message saved: family={FamilyId} chat={ChatId} msg={MsgId} type={Type}",
                familyId, chat.Id, message.Id, messageType);

            // Publish SSE to all active family members
            var memberUserIds = await _db.FamilyMembers
                .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            var ssePayload = JsonSerializer.Serialize(new
            {
                chatId = chat.Id,
                messageId = message.Id,
                content = text,
                eventType = messageType.ToString(),
                correlationId = correlationId,
            });

            var sseEvent = new SseEvent("chat_message_created", ssePayload);

            foreach (var userId in memberUserIds)
            {
                try
                {
                    await _sse.PublishAsync(userId, sseEvent, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish SSE for user {UserId}", userId);
                }
            }

            return message.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatSynchronizationService failed for family {FamilyId} type {Type}", familyId, messageType);
            return null;
        }
    }

    private static MessageTypeEnum ResolveMessageType(ProactiveChatMessageType type) => type switch
    {
        ProactiveChatMessageType.MorningBriefing => MessageTypeEnum.Recommendation,
        ProactiveChatMessageType.MedicationReminder => MessageTypeEnum.Reminder,
        ProactiveChatMessageType.WeatherAlert => MessageTypeEnum.Alert,
        ProactiveChatMessageType.AQIAlert => MessageTypeEnum.Alert,
        ProactiveChatMessageType.PollenAlert => MessageTypeEnum.Alert,
        ProactiveChatMessageType.LeaveHomeChecklist => MessageTypeEnum.Reminder,
        ProactiveChatMessageType.ProactiveRecommendation => MessageTypeEnum.Recommendation,
        ProactiveChatMessageType.EscalationAlert => MessageTypeEnum.Alert,
        ProactiveChatMessageType.VoiceActionCreated => MessageTypeEnum.Reminder,
        ProactiveChatMessageType.HealthAlert => MessageTypeEnum.Alert,
        _ => MessageTypeEnum.Message,
    };
}
