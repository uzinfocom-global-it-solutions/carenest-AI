using Backend.Domain.Enums;

namespace Backend.Application.Weather.Models;

public record WeatherSnapshotResult(
    int Id,
    string LocationKey,
    double? Latitude,
    double? Longitude,
    double TemperatureC,
    double? FeelsLikeC,
    int? HumidityPercent,
    double? WindKmh,
    double? UvIndex,
    string? Aqi,
    string? PollenLevel,
    WeatherConditionEnum WeatherCondition,
    DateTimeOffset ForecastTime,
    DateTimeOffset CollectedAt);
