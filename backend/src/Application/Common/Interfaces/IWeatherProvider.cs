using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

/// <summary>
/// External-data abstraction for weather + air-quality + pollen + UV.
/// Default impl is a deterministic stub. Production swap: AirVisual, Tomorrow.io, OpenWeather One Call API, etc.
/// </summary>
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
