using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IWeatherProvider
{
    Task<WeatherProviderResult> FetchAsync(
        string locationKey,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default);
}

public record WeatherProviderResult(
    double TemperatureC,
    double? FeelsLikeC,
    int? HumidityPercent,
    double? WindKmh,
    double? UvIndex,
    string? Aqi,
    string? PollenLevel,
    WeatherConditionEnum WeatherCondition,
    DateTimeOffset ForecastTime,
    string? RawPayload);
