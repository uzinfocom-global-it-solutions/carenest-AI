using Backend.Application.Users.Commands.UpdateUserSettings;
using Backend.Application.Users.Models;
using Backend.Application.Users.Queries.GetUserSettings;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static string? RoutePrefix => "/users";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetSettings, "me/settings");
        groupBuilder.MapPut(UpdateSettings, "me/settings");
    }

    [EndpointSummary("Get my settings")]
    [EndpointDescription("Returns the current user's preferences (language, timezone, quiet hours, notification level, etc.). Auto-creates defaults on first access.")]
    public static async Task<IResult> GetSettings(
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new GetUserSettingsQuery(userId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Update my settings")]
    [EndpointDescription("Partially updates the current user's preferences. Only fields supplied in the body are changed.")]
    public static async Task<IResult> UpdateSettings(
        UpdateUserSettingDto body,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new UpdateUserSettingsCommand(userId, body), ct);
        return Results.Ok(result);
    }
}
