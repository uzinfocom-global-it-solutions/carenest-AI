using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

[Authorize]
public class AIFeed : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetAiFeed, "ai-feed");
        groupBuilder.MapGet(GetAiMemory, "ai-memory");
    }

    [EndpointSummary("Get AI Feed")]
    [EndpointDescription("Returns the last 50 proactive AI messages (Ai sender) from the family's active chat.")]
    public static async Task<IResult> GetAiFeed(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct,
        int limit = 50)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is null) return Results.NotFound("Family not found");

        // Get the family's earliest chat (primary family chat)
        var chatId = await db.Chats
            .Where(c => c.FamilyId == familyId.Value)
            .OrderBy(c => c.CreatedAt)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (chatId is null) return Results.Ok(Array.Empty<AiFeedMessageDto>());

        var clampedLimit = Math.Clamp(limit, 1, 200);

        var messages = await db.ChatMessages
            .Where(m => m.ChatId == chatId.Value && m.SenderType == SenderTypeEnum.Ai)
            .OrderByDescending(m => m.CreatedAt)
            .Take(clampedLimit)
            .Select(m => new AiFeedMessageDto(
                m.Id,
                m.ChatId,
                m.Content,
                m.MessageType.ToString(),
                m.ParsedIntent,
                m.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(messages);
    }

    [EndpointSummary("Get AI Memory")]
    [EndpointDescription("Returns all non-expired AI memory entries for the current user's family.")]
    public static async Task<IResult> GetAiMemory(
        IApplicationDbContext db,
        IAIMemoryService memoryService,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is null) return Results.NotFound("Family not found");

        var memories = await memoryService.GetFamilyMemoryAsync(familyId.Value, ct);
        return Results.Ok(memories.Select(m => new AIMemoryDto(
            m.Id,
            m.Key,
            m.Value,
            m.MemoryType.ToString(),
            m.RelevanceScore,
            m.ObservationCount,
            m.LastObservedAt,
            m.ExpiresAt,
            m.CreatedAt)));
    }
}

public record AiFeedMessageDto(
    int Id,
    int ChatId,
    string Content,
    string MessageType,
    string? ParsedIntent,
    DateTimeOffset CreatedAt);

public record AIMemoryDto(
    int Id,
    string Key,
    string Value,
    string MemoryType,
    int RelevanceScore,
    int ObservationCount,
    DateTimeOffset LastObservedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
