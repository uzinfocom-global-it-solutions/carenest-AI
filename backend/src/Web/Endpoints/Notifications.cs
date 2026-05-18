using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

public class Notifications : IEndpointGroup
{
    public static string? RoutePrefix => "/notifications";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(ListMine, "");
        groupBuilder.MapPost(MarkRead, "{notificationId:int}/read");
        groupBuilder.MapPost(MarkAllRead, "read-all");
    }

    [EndpointSummary("List My Notifications")]
    [EndpointDescription("Returns all in-app notification deliveries for the caller, newest first. Joins recommendation title/message so the client doesn't need a second fetch.")]
    public static async Task<IResult> ListMine(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var items = await db.NotificationDeliveries
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Id)
            .Take(100)
            .Select(d => new NotificationItem(
                d.Id,
                d.RecommendationId,
                d.Recommendation.Title,
                d.Recommendation.Message,
                d.Recommendation.RecommendationType.ToString(),
                d.Recommendation.Priority,
                d.Recommendation.ChildId,
                d.DeliveryStatus.ToString(),
                d.DeliveredAt,
                d.OpenedAt,
                d.Recommendation.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    [EndpointSummary("Mark Notification Read")]
    [EndpointDescription("Marks a single notification as Opened (idempotent).")]
    public static async Task<IResult> MarkRead(
        int notificationId,
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var delivery = await db.NotificationDeliveries
            .FirstOrDefaultAsync(d => d.Id == notificationId && d.UserId == userId, ct);
        if (delivery == null) return Results.NotFound();

        if (delivery.OpenedAt == null)
        {
            delivery.DeliveryStatus = DeliveryStatusEnum.Opened;
            delivery.OpenedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Results.NoContent();
    }

    [EndpointSummary("Mark All Notifications Read")]
    [EndpointDescription("Marks every unopened notification for the caller as Opened.")]
    public static async Task<IResult> MarkAllRead(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var unread = await db.NotificationDeliveries
            .Where(d => d.UserId == userId && d.OpenedAt == null)
            .ToListAsync(ct);

        foreach (var d in unread)
        {
            d.DeliveryStatus = DeliveryStatusEnum.Opened;
            d.OpenedAt = now;
        }
        if (unread.Count > 0) await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

public record NotificationItem(
    int Id,
    int RecommendationId,
    string Title,
    string Message,
    string Type,
    string Priority,
    int ChildId,
    string Status,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? OpenedAt,
    DateTimeOffset CreatedAt
);
