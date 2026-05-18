using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Weather;

/// <summary>
/// Deterministic in-memory weather provider for dev / tests / environments without an external API key.
/// Outputs are derived from a hash of the location key + the current hour, so calls within the same
/// hour produce the same snapshot (useful for caching tests).
/// </summary>
internal sealed class StubWeatherProvider : IWeatherProvider
{
    public Task<WeatherProviderResult> FetchAsync(
        string locationKey,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var seed = HashCode.Combine(locationKey, now.Year, now.DayOfYear, now.Hour);
        var rng = new Random(seed);

        var temp = Math.Round(rng.NextDouble() * 35 - 5, 1); // -5 .. 30
        var humidity = rng.Next(20, 95);
        var wind = Math.Round(rng.NextDouble() * 30, 1);
        var uv = Math.Round(rng.NextDouble() * 11, 1);
        var aqi = rng.Next(20, 200).ToString();
        var pollen = rng.Next(0, 3) switch { 0 => "low", 1 => "moderate", _ => "high" };

        var condition = (WeatherConditionEnum)rng.Next(0, 6);

        return Task.FromResult(new WeatherProviderResult(
            TemperatureC: temp,
            FeelsLikeC: temp - rng.NextDouble() * 3,
            HumidityPercent: humidity,
            WindKmh: wind,
            UvIndex: uv,
            Aqi: aqi,
            PollenLevel: pollen,
            WeatherCondition: condition,
            ForecastTime: now,
            RawPayload: $"{{\"stub\":true,\"loc\":\"{locationKey}\",\"hour\":{now.Hour}}}"));
    }
}
