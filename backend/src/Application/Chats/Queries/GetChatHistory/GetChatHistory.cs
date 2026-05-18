using Backend.Application.Chats.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Chats.Queries.GetChatHistory;

public record GetChatHistoryQuery(
    int ChatId,
    string RequestingUserId,
    int Limit) : IRequest<IReadOnlyList<ChatMessageResult>>;

public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, IReadOnlyList<ChatMessageResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public GetChatHistoryQueryHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<ChatMessageResult>> Handle(
        GetChatHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var chat = await _context.Chats.FindAsync([request.ChatId], cancellationToken)
            ?? throw new NotFoundException(nameof(Chat), request.ChatId);

        await _authorization.AssertMemberAsync(chat.FamilyId, request.RequestingUserId, cancellationToken);

        return await _context.ChatMessages
            .Where(m => m.ChatId == request.ChatId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(request.Limit)
            .Select(m => new ChatMessageResult(
                m.Id, m.ChatId, m.UserId, m.SenderType, m.MessageType,
                m.Content, m.AiResponse, m.ParsedIntent, m.Successful, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
