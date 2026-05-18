using Backend.Application.Recommendations.Commands.GenerateRecommendations;
using Backend.Application.Recommendations.Commands.SubmitFeedback;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Recommendations : IEndpointGroup
{
    public static string? RoutePrefix => "/recommendations";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(Generate, "generate/{childId}");
        groupBuilder.MapPost(Feedback, "{recommendationId}/feedback");
    }

    [EndpointSummary("Generate Recommendations")]
    [EndpointDescription("Runs the recommendation engine for a child: evaluates weather, sensitivity, routines, and calendar events.")]
    public static async Task<IResult> Generate(
        int childId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var results = await mediator.Send(new GenerateRecommendationsCommand(childId, userId), ct);
        return Results.Ok(results);
    }

    [EndpointSummary("Submit Feedback")]
    [EndpointDescription("Records user feedback on a recommendation.")]
    public static async Task<IResult> Feedback(
        int recommendationId,
        FeedbackRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new SubmitFeedbackCommand(recommendationId, userId, req.FeedbackType), ct);
        return Results.Ok(result);
    }
}

public record FeedbackRequest(string FeedbackType);
