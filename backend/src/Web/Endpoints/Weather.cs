using Backend.Application.Weather.Queries.GetCurrentWeather;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Weather : IEndpointGroup
{
    public static string? RoutePrefix => "/weather";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetCurrent, "current");
    }

    [EndpointSummary("Get Current Weather")]
    [EndpointDescription("Returns the latest weather snapshot for a location. Reads from a 60s distributed cache, then DB (30 min staleness), then upstream provider as a fallback.")]
    public static async Task<IResult> GetCurrent(
        string locationKey,
        ISender mediator,
        CancellationToken ct,
        double? latitude = null,
        double? longitude = null)
    {
        var result = await mediator.Send(
            new GetCurrentWeatherQuery(locationKey, latitude, longitude), ct);
        return Results.Ok(result);
    }
}
