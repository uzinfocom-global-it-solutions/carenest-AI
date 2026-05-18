using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Web.Infrastructure;

namespace Backend.Web.Endpoints;

[Microsoft.AspNetCore.Authorization.Authorize]
public class Monitoring : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/families";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetActiveSessions, "{familyId}/monitoring/sessions");
        groupBuilder.MapPost(ResolveSession, "{familyId}/monitoring/sessions/{sessionId}/resolve");
    }

    [EndpointSummary("Get Active Monitoring Sessions")]
    [EndpointDescription("Returns all unresolved health monitoring sessions for the family, ordered by risk score descending.")]
    public static async Task<IResult> GetActiveSessions(
        int familyId,
        HttpContext ctx,
        IHealthMonitoringService monitoringService,
        IFamilyAuthorization authorization,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await authorization.AssertMemberAsync(familyId, userId, ct);

        var sessions = await monitoringService.GetActiveSessionsForFamilyAsync(familyId, ct);

        var result = sessions.Select(s => new
        {
            s.Id,
            s.FamilyId,
            s.ChildId,
            childName = s.Child?.DisplayName,
            issueType = s.IssueType.ToString(),
            severity  = s.Severity.ToString(),
            status    = s.Status.ToString(),
            s.RiskScore,
            s.StartedAt,
            s.LastUpdatedAt,
            s.NextFollowUpAt,
            s.InitialSymptomDescription,
            s.FollowUpCount,
            s.MissedFollowUps,
            s.IsResolved,
        });

        return Results.Ok(result);
    }

    [EndpointSummary("Resolve Monitoring Session")]
    [EndpointDescription("Marks a health monitoring session as resolved.")]
    public static async Task<IResult> ResolveSession(
        int familyId,
        int sessionId,
        ResolveSessionRequest request,
        HttpContext ctx,
        IHealthMonitoringService monitoringService,
        IFamilyAuthorization authorization,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await authorization.AssertMemberAsync(familyId, userId, ct);

        await monitoringService.ResolveSessionAsync(
            sessionId,
            request.Summary ?? "Resolved by user.",
            ct);

        return Results.NoContent();
    }
}

public record ResolveSessionRequest(string? Summary);
