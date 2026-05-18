using Backend.Application.Chats.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Chats.Commands.CreateChat;

public record CreateChatCommand(
    int FamilyId,
    string CreatedByUserId,
    string ChatType,
    string? Title) : IRequest<ChatResult>;

public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, ChatResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public CreateChatCommandHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<ChatResult> Handle(CreateChatCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertMemberAsync(request.FamilyId, request.CreatedByUserId, cancellationToken);

        if (!Enum.TryParse<ChatTypeEnum>(request.ChatType, ignoreCase: true, out var type))
            throw new ValidationException([new("chatType", $"Invalid chat type '{request.ChatType}'.")]);

        var chat = new Chat
        {
            FamilyId = request.FamilyId,
            CreatedByUserId = request.CreatedByUserId,
            ChatType = type,
            Title = request.Title,
        };

        _context.Chats.Add(chat);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatResult(
            chat.Id, chat.FamilyId, chat.CreatedByUserId,
            chat.ChatType, chat.Title, chat.CreatedAt);
    }
}
