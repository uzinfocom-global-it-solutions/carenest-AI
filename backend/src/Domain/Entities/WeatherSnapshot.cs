using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class WeatherSnapshot : BaseEntity
{
    public required string LocationKey { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public required double TemperatureC { get; set; }
    public double? FeelsLikeC { get; set; }
    public int? HumidityPercent { get; set; }
    public double? WindKmh { get; set; }
    public double? UvIndex { get; set; }
    public string? Aqi { get; set; }
    public string? PollenLevel { get; set; }
    public required WeatherConditionEnum WeatherCondition { get; set; }
    public required DateTimeOffset ForecastTime { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RawPayload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
