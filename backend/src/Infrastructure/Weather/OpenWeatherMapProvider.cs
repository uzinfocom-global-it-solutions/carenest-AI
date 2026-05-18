using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Weather;

/// <summary>
/// OpenWeatherMap-backed weather provider. AQI/pollen/UV are not in the Free tier — those fields
/// stay null here. Use a richer provider (AirVisual, Tomorrow.io, etc.) to populate them.
/// </summary>
internal sealed class OpenWeatherMapProvider : IWeatherProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenWeatherMapProvider> _logger;
    private readonly string _apiKey;

    public OpenWeatherMapProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenWeatherMapProvider> logger)
    {
        _http = httpClientFactory.CreateClient("weather");
        _logger = logger;
        _apiKey = configuration["Weather:ApiKey"] ?? string.Empty;
    }

    public async Task<WeatherProviderResult> FetchAsync(
        string locationKey,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(locationKey, latitude, longitude);
        var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return Normalise(raw);
    }

    private string BuildUrl(string locationKey, double? lat, double? lon)
    {
        if (lat.HasValue && lon.HasValue)
            return $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric";
        return $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(locationKey)}&appid={_apiKey}&units=metric";
    }

    private static WeatherProviderResult Normalise(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var main = root.GetProperty("main");
        var wind = root.GetProperty("wind");
        var weatherArr = root.GetProperty("weather");
        var conditionStr = weatherArr[0].GetProperty("main").GetString() ?? "Cloudy";

        var condition = conditionStr.ToLower() switch
        {
            "clear" => WeatherConditionEnum.Sunny,
            "clouds" => WeatherConditionEnum.Cloudy,
            "rain" or "drizzle" => WeatherConditionEnum.Rainy,
            "snow" => WeatherConditionEnum.Snowy,
            "thunderstorm" => WeatherConditionEnum.Storm,
            _ when conditionStr.Contains("wind", StringComparison.OrdinalIgnoreCase) => WeatherConditionEnum.Windy,
            _ => WeatherConditionEnum.Cloudy,
        };

        return new WeatherProviderResult(
            TemperatureC: main.GetProperty("temp").GetDouble(),
            FeelsLikeC: main.GetProperty("feels_like").GetDouble(),
            HumidityPercent: main.GetProperty("humidity").GetInt32(),
            WindKmh: wind.GetProperty("speed").GetDouble() * 3.6,
            UvIndex: null, // not in Free tier /weather endpoint
            Aqi: null,     // requires /air_pollution endpoint
            PollenLevel: null,
            WeatherCondition: condition,
            ForecastTime: DateTimeOffset.UtcNow,
            RawPayload: raw);
    }
}
