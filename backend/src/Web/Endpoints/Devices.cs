using Backend.Application.Common.Interfaces;
using Backend.Web.Infrastructure;

namespace Backend.Web.Endpoints;

public class Devices : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/devices";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(Register, "register");
        groupBuilder.MapPost(Unregister, "unregister");
        groupBuilder.MapGet(List, "");
    }

    [EndpointSummary("Register Device Token")]
    [EndpointDescription("Registers or updates an FCM device token for the authenticated user.")]
    public static async Task<IResult> Register(
        RegisterDeviceRequest request,
        IDeviceTokenService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var token = await service.RegisterAsync(
            userId, request.Token, request.Platform, request.DeviceName, ct);

        return Results.Ok(new DeviceTokenDto(token.Id, token.Token, token.Platform, token.DeviceName, token.LastSeenAt));
    }

    [EndpointSummary("Unregister Device Token")]
    [EndpointDescription("Deactivates the given FCM token for the current user.")]
    public static async Task<IResult> Unregister(
        UnregisterDeviceRequest request,
        IDeviceTokenService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        await service.UnregisterAsync(userId, request.Token, ct);
        return Results.NoContent();
    }

    [EndpointSummary("List My Device Tokens")]
    [EndpointDescription("Returns all active FCM device tokens registered for the current user.")]
    public static async Task<IResult> List(
        IDeviceTokenService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var tokens = await service.GetUserTokensAsync(userId, ct);
        return Results.Ok(tokens.Select(t =>
            new DeviceTokenDto(t.Id, t.Token, t.Platform, t.DeviceName, t.LastSeenAt)));
    }
}

public record RegisterDeviceRequest(string Token, string Platform, string? DeviceName);
public record UnregisterDeviceRequest(string Token);
public record DeviceTokenDto(int Id, string Token, string Platform, string? DeviceName, DateTimeOffset LastSeenAt);
