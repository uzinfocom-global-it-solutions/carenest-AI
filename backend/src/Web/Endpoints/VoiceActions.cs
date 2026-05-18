using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

public class VoiceActions : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/voice-actions";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetPending, "pending");
        groupBuilder.MapGet(GetTimeline, "timeline");
        groupBuilder.MapPost(Confirm, "{id:int}/confirm");
        groupBuilder.MapPost(Skip, "{id:int}/skip");
        groupBuilder.MapPost(CreateManual, "");
    }

    [EndpointSummary("Get Pending Voice Actions")]
    public static async Task<IResult> GetPending(
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var actions = await service.GetPendingAsync(userId, ct);
        return Results.Ok(actions.Select(ToDto));
    }

    [EndpointSummary("Get Family Voice Action Timeline")]
    public static async Task<IResult> GetTimeline(
        IApplicationDbContext db,
        IVoiceActionService service,
        HttpContext ctx,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.Ok(Array.Empty<object>());

        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = to ?? DateTimeOffset.UtcNow;
        var actions = await service.GetFamilyTimelineAsync(familyId, fromDate, toDate, ct);
        return Results.Ok(actions.Select(ToDto));
    }

    [EndpointSummary("Confirm Voice Action")]
    public static async Task<IResult> Confirm(
        int id,
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        await service.ConfirmAsync(id, userId, ct);
        return Results.NoContent();
    }

    [EndpointSummary("Skip Voice Action")]
    public static async Task<IResult> Skip(
        int id,
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        await service.SkipAsync(id, userId, ct);
        return Results.NoContent();
    }

    [EndpointSummary("Create Manual Voice Action")]
    public static async Task<IResult> CreateManual(
        CreateVoiceActionRequest req,
        IApplicationDbContext db,
        IVoiceActionService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId == 0) return Results.BadRequest("User not in any family");

        var action = await service.CreateAsync(
            familyId, userId,
            req.Type, req.Text, req.Priority,
            req.RequiresConfirmation, req.EscalationEnabled,
            req.ScheduledAt, req.Metadata, null, ct);

        if (action is null) return Results.Ok(new { deduplicated = true });
        return Results.Created($"/api/v1/voice-actions/{action.Id}", ToDto(action));
    }

    private static VoiceActionDto ToDto(Domain.Entities.VoiceAction a) => new(
        a.Id, a.FamilyId, a.UserId,
        a.Type.ToString(), a.Priority.ToString(),
        a.Text, a.Status.ToString(),
        a.RequiresConfirmation, a.EscalationEnabled,
        a.ScheduledAt, a.DeliveredAt, a.ConfirmedAt,
        a.EscalationStep, a.CreatedAt);
}

public record CreateVoiceActionRequest(
    VoiceActionType Type,
    string Text,
    NotificationPriority Priority = NotificationPriority.Normal,
    bool RequiresConfirmation = false,
    bool EscalationEnabled = false,
    DateTimeOffset? ScheduledAt = null,
    string? Metadata = null);

public record VoiceActionDto(
    int Id, int FamilyId, string UserId,
    string Type, string Priority,
    string Text, string Status,
    bool RequiresConfirmation, bool EscalationEnabled,
    DateTimeOffset? ScheduledAt, DateTimeOffset? DeliveredAt, DateTimeOffset? ConfirmedAt,
    int EscalationStep, DateTimeOffset CreatedAt);
