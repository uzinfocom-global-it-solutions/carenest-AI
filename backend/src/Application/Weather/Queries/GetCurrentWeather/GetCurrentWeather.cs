using Backend.Application.Common.Interfaces;
using Backend.Application.Weather.Models;
using Backend.Domain.Entities;

namespace Backend.Application.Weather.Queries.GetCurrentWeather;

public record GetCurrentWeatherQuery(
    string LocationKey,
    double? Latitude,
    double? Longitude) : IRequest<WeatherSnapshotResult>;

public class GetCurrentWeatherQueryHandler : IRequestHandler<GetCurrentWeatherQuery, WeatherSnapshotResult>
{
    private readonly IWeatherAdapter _weather;

    public GetCurrentWeatherQueryHandler(IWeatherAdapter weather) => _weather = weather;

    public async Task<WeatherSnapshotResult> Handle(GetCurrentWeatherQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await _weather.GetLatestAsync(request.LocationKey, cancellationToken)
                    ?? await _weather.FetchAndStoreAsync(
                            request.LocationKey, request.Latitude, request.Longitude, cancellationToken);

        return Map(snapshot);
    }

    internal static WeatherSnapshotResult Map(WeatherSnapshot s) => new(
        s.Id, s.LocationKey, s.Latitude, s.Longitude,
        s.TemperatureC, s.FeelsLikeC, s.HumidityPercent, s.WindKmh, s.UvIndex,
        s.Aqi, s.PollenLevel, s.WeatherCondition,
        s.ForecastTime, s.CollectedAt);
}
