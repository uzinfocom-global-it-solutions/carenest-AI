using Backend.Application.Families.Commands.AcceptInvitation;
using Backend.Application.Families.Commands.AddFamilyMember;
using Backend.Application.Families.Commands.CreateFamily;
using Backend.Application.Families.Commands.RemoveMember;
using Backend.Application.Families.Commands.UpdateMemberRole;
using Backend.Application.Families.Queries.GetMyFamily;
using Backend.Web.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Web.Endpoints;

[Authorize]
public class Families : IEndpointGroup
{
    public static string? RoutePrefix => "/families";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetMine, "mine");
        groupBuilder.MapPost(Create, "");
        groupBuilder.MapPost(AddMember, "{familyId}/members");
        groupBuilder.MapPost(AcceptInvitation, "{familyId}/invitation/accept");
        groupBuilder.MapPut(UpdateMemberRole, "{familyId}/members/{targetUserId}/role");
        groupBuilder.MapDelete(RemoveMember, "{familyId}/members/{targetUserId}");
    }

    [EndpointSummary("Get My Family")]
    [EndpointDescription("Returns the active family the requesting user belongs to, or 404 if none.")]
    public static async Task<IResult> GetMine(ISender mediator, HttpContext ctx, CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var family = await mediator.Send(new GetMyFamilyQuery(userId), ct);
        return family is null ? Results.NotFound() : Results.Ok(family);
    }

    [EndpointSummary("Create Family")]
    [EndpointDescription("Creates a new family and auto-assigns the requesting user as an active parent.")]
    public static async Task<IResult> Create(
        CreateFamilyRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new CreateFamilyCommand(req.Name, userId, req.LocationKey, req.Latitude, req.Longitude), ct);
        return Results.Created($"/families/{result.Id}", result);
    }

    [EndpointSummary("Add Family Member")]
    [EndpointDescription("Invites a user to a family. Requires parent role. Member starts in Invited status.")]
    public static async Task<IResult> AddMember(
        int familyId,
        AddMemberRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new AddFamilyMemberCommand(familyId, req.UserId, req.Role, userId), ct);
        return Results.Ok();
    }

    [EndpointSummary("Accept Invitation")]
    [EndpointDescription("Accepts an outstanding family-membership invitation. Caller must be the invited user.")]
    public static async Task<IResult> AcceptInvitation(
        int familyId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new AcceptInvitationCommand(familyId, userId), ct);
        return Results.Ok();
    }

    [EndpointSummary("Update Member Role")]
    [EndpointDescription("Changes a family member's role. Requires parent role. Cannot demote the last active parent.")]
    public static async Task<IResult> UpdateMemberRole(
        int familyId,
        string targetUserId,
        UpdateRoleRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new UpdateMemberRoleCommand(familyId, targetUserId, req.Role, userId), ct);
        return Results.Ok();
    }

    [EndpointSummary("Remove Member")]
    [EndpointDescription("Soft-removes a family member (sets status=Disabled). Requires parent role. Cannot remove the last active parent.")]
    public static async Task<IResult> RemoveMember(
        int familyId,
        string targetUserId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new RemoveMemberCommand(familyId, targetUserId, userId), ct);
        return Results.Ok();
    }
}

public record CreateFamilyRequest(string Name, string? LocationKey, double? Latitude, double? Longitude);
public record AddMemberRequest(string UserId, string Role);
public record UpdateRoleRequest(string Role);
