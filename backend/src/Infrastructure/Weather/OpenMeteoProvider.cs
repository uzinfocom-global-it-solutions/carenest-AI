using System.Globalization;
using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Weather;

/// <summary>
/// Real weather via Open-Meteo — no API key required, free for non-commercial use.
/// Uses two endpoints: forecast (temp/wind/uv/condition) + air-quality (AQI/pollen).
/// If lat/lon are missing, geocodes the locationKey first.
/// </summary>
internal sealed class OpenMeteoProvider : IWeatherProvider
{
    private const string ForecastBase = "https://api.open-meteo.com/v1/forecast";
    private const string AirQualityBase = "https://air-quality-api.open-meteo.com/v1/air-quality";
    private const string GeocodingBase = "https://geocoding-api.open-meteo.com/v1/search";

    private readonly HttpClient _http;
    private readonly ILogger<OpenMeteoProvider> _logger;

    public OpenMeteoProvider(IHttpClientFactory httpClientFactory, ILogger<OpenMeteoProvider> logger)
    {
        _http = httpClientFactory.CreateClient("weather");
        _logger = logger;
    }

    public async Task<WeatherProviderResult> FetchAsync(
        string locationKey,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude is null || longitude is null)
        {
            var geo = await GeocodeAsync(locationKey, cancellationToken);
            latitude = geo?.lat;
            longitude = geo?.lon;
        }

        if (latitude is null || longitude is null)
            throw new InvalidOperationException($"Could not resolve coordinates for '{locationKey}'.");

        var lat = latitude.Value.ToString("F4", CultureInfo.InvariantCulture);
        var lon = longitude.Value.ToString("F4", CultureInfo.InvariantCulture);

        var forecastUrl = $"{ForecastBase}?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,uv_index" +
            "&timezone=auto";

        var airUrl = $"{AirQualityBase}?latitude={lat}&longitude={lon}" +
            "&current=european_aqi,grass_pollen,birch_pollen" +
            "&timezone=auto";

        // The air-quality endpoint is optional — if it fails (e.g. coverage gap) we still want
        // the temperature reading.
        var forecastTask = _http.GetStringAsync(forecastUrl, cancellationToken);
        var airTask = SafeGetStringAsync(airUrl, cancellationToken);
        var forecast = await forecastTask;
        var air = await airTask;

        return Normalise(forecast, air);
    }

    private async Task<(double lat, double lon)?> GeocodeAsync(string locationKey, CancellationToken ct)
    {
        try
        {
            var url = $"{GeocodingBase}?name={Uri.EscapeDataString(locationKey)}&count=1&language=en&format=json";
            var raw = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("results", out var results)) return null;
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;
            var first = results[0];
            return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding failed for {LocationKey}", locationKey);
            return null;
        }
    }

    private async Task<string?> SafeGetStringAsync(string url, CancellationToken ct)
    {
        try { return await _http.GetStringAsync(url, ct); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Optional weather call failed: {Url}", url);
            return null;
        }
    }

    private static WeatherProviderResult Normalise(string forecastJson, string? airJson)
    {
        using var fdoc = JsonDocument.Parse(forecastJson);
        var current = fdoc.RootElement.GetProperty("current");

        var temp = current.GetProperty("temperature_2m").GetDouble();
        var feels = current.TryGetProperty("apparent_temperature", out var fl) ? fl.GetDouble() : (double?)null;
        var humidity = current.TryGetProperty("relative_humidity_2m", out var h) ? h.GetInt32() : (int?)null;
        var windMs = current.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : 0.0;
        var uv = current.TryGetProperty("uv_index", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetDouble()
            : (double?)null;
        var weatherCode = current.TryGetProperty("weather_code", out var wc) ? wc.GetInt32() : 3;

        string? aqi = null;
        string? pollen = null;
        if (airJson is not null)
        {
            try
            {
                using var adoc = JsonDocument.Parse(airJson);
                var ac = adoc.RootElement.GetProperty("current");
                if (ac.TryGetProperty("european_aqi", out var aqiEl) && aqiEl.ValueKind == JsonValueKind.Number)
                {
                    aqi = ((int)Math.Round(aqiEl.GetDouble())).ToString(CultureInfo.InvariantCulture);
                }
                pollen = SummarisePollen(ac);
            }
            catch { /* tolerate malformed air payload */ }
        }

        return new WeatherProviderResult(
            TemperatureC: temp,
            FeelsLikeC: feels,
            HumidityPercent: humidity,
            WindKmh: Math.Round(windMs * 3.6, 1),
            UvIndex: uv,
            Aqi: aqi,
            PollenLevel: pollen,
            WeatherCondition: MapWmoCode(weatherCode),
            ForecastTime: DateTimeOffset.UtcNow,
            RawPayload: forecastJson);
    }

    private static string? SummarisePollen(JsonElement ac)
    {
        double? grass = ac.TryGetProperty("grass_pollen", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDouble() : null;
        double? birch = ac.TryGetProperty("birch_pollen", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetDouble() : null;
        var max = new[] { grass, birch }.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(double.NaN).Max();
        if (double.IsNaN(max)) return null;
        return max switch
        {
            < 5 => "low",
            < 25 => "moderate",
            _ => "high",
        };
    }

    /// <summary>WMO weather codes → our enum. See https://open-meteo.com/en/docs.</summary>
    private static WeatherConditionEnum MapWmoCode(int code) => code switch
    {
        0 => WeatherConditionEnum.Sunny,
        1 or 2 or 3 => WeatherConditionEnum.Cloudy,
        45 or 48 => WeatherConditionEnum.Cloudy,
        51 or 53 or 55 or 56 or 57 => WeatherConditionEnum.Rainy,
        61 or 63 or 65 or 66 or 67 => WeatherConditionEnum.Rainy,
        80 or 81 or 82 => WeatherConditionEnum.Rainy,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherConditionEnum.Snowy,
        95 or 96 or 99 => WeatherConditionEnum.Storm,
        _ => WeatherConditionEnum.Cloudy,
    };
}
