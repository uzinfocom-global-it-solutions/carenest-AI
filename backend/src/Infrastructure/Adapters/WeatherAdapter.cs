using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Adapters;

/// <summary>
/// Orchestrates: DB lookup (staleness window) → IWeatherProvider call → persistence.
/// External-data details live in the IWeatherProvider implementation; this class only
/// knows how to read/write WeatherSnapshot rows.
/// </summary>
public class WeatherAdapter : IWeatherAdapter
{
    // Snapshots fresher than this are considered usable for read-paths.
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(30);

    private readonly IApplicationDbContext _context;
    private readonly IWeatherProvider _provider;
    private readonly ILogger<WeatherAdapter> _logger;

    public WeatherAdapter(
        IApplicationDbContext context,
        IWeatherProvider provider,
        ILogger<WeatherAdapter> logger)
    {
        _context = context;
        _provider = provider;
        _logger = logger;
    }

    public async Task<WeatherSnapshot?> GetLatestAsync(string locationKey, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(StalenessThreshold);
        return await _context.WeatherSnapshots
            .Where(w => w.LocationKey == locationKey && w.CollectedAt >= cutoff)
            .OrderByDescending(w => w.CollectedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<WeatherSnapshot> FetchAndStoreAsync(
        string locationKey, double? latitude, double? longitude, CancellationToken ct = default)
    {
        WeatherProviderResult fetched;
        try
        {
            fetched = await _provider.FetchAsync(locationKey, latitude, longitude, ct);
            _logger.LogDebug("[Weather] Provider fetch OK for {Key}: {Temp}°C", locationKey, fetched.TemperatureC);
        }
        catch (Exception ex)
        {
            // Log at Error so a missing/invalid API key is immediately visible in logs.
            _logger.LogError(ex,
                "[Weather] Provider FAILED for key={Key} lat={Lat} lon={Lon} — storing fallback 20°C. " +
                "Check Weather:ApiKey in appsettings.",
                locationKey, latitude, longitude);
            fetched = Fallback();
        }

        var snapshot = new WeatherSnapshot
        {
            LocationKey = locationKey,
            Latitude = latitude,
            Longitude = longitude,
            TemperatureC = fetched.TemperatureC,
            FeelsLikeC = fetched.FeelsLikeC,
            HumidityPercent = fetched.HumidityPercent,
            WindKmh = fetched.WindKmh,
            UvIndex = fetched.UvIndex,
            Aqi = fetched.Aqi,
            PollenLevel = fetched.PollenLevel,
            WeatherCondition = fetched.WeatherCondition,
            ForecastTime = fetched.ForecastTime,
            RawPayload = fetched.RawPayload,
        };

        _context.WeatherSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(ct);
        return snapshot;
    }

    private static WeatherProviderResult Fallback() => new(
        TemperatureC: 20,
        FeelsLikeC: null,
        HumidityPercent: null,
        WindKmh: null,
        UvIndex: null,
        Aqi: null,
        PollenLevel: null,
        WeatherCondition: WeatherConditionEnum.Cloudy,
        ForecastTime: DateTimeOffset.UtcNow,
        RawPayload: null);
}
