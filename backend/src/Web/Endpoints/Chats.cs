using Backend.Application.Chats.Commands.ConfirmProposal;
using Backend.Application.Chats.Commands.CreateChat;
using Backend.Application.Chats.Commands.SendMessage;
using Backend.Application.Chats.Queries.GetChatHistory;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Chats : IEndpointGroup
{
    public static string? RoutePrefix => "/chats";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(CreateChat, "");
        groupBuilder.MapPost(SendMessage, "{chatId}/messages");
        groupBuilder.MapGet(GetHistory, "{chatId}/messages");
        groupBuilder.MapPost(ConfirmProposal, "{chatId:int}/messages/{messageId:int}/confirm");
    }

    [EndpointSummary("Create Chat")]
    [EndpointDescription("Creates a new chat session for a family.")]
    public static async Task<IResult> CreateChat(
        CreateChatRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new CreateChatCommand(req.FamilyId, userId, req.ChatType, req.Title), ct);
        return Results.Created($"/chats/{result.Id}", result);
    }

    [EndpointSummary("Send Message")]
    [EndpointDescription("Sends a user message. Triggers AI intent parsing and optional recommendation generation.")]
    public static async Task<IResult> SendMessage(
        int chatId,
        SendMessageRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new SendMessageCommand(chatId, userId, req.Content, req.InputMode, req.Locale ?? "en"), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Get Chat History")]
    [EndpointDescription("Returns recent messages for a chat session.")]
    public static async Task<IResult> GetHistory(
        int chatId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct,
        int limit = 50)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var messages = await mediator.Send(
            new GetChatHistoryQuery(chatId, userId, limit), ct);
        return Results.Ok(messages);
    }

    [EndpointSummary("Confirm Proposal")]
    [EndpointDescription("Confirms an AI-suggested action proposal attached to a chat message and executes it (add child, event, routine, note, sensitivity update). Pass an `edits` object to override the proposal parameters before they're applied.")]
    public static async Task<IResult> ConfirmProposal(
        int chatId,
        int messageId,
        ConfirmProposalRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new ConfirmProposalCommand(chatId, messageId, userId, req.Edits), ct);
        return Results.Ok(result);
    }
}

public record CreateChatRequest(int FamilyId, string ChatType, string? Title);
public record SendMessageRequest(string Content, string InputMode, string? Locale);
public record ConfirmProposalRequest(string? Edits);
