using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Adapters;

internal sealed class CachedWeatherAdapter : IWeatherAdapter
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly WeatherAdapter _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedWeatherAdapter> _logger;

    public CachedWeatherAdapter(
        WeatherAdapter inner,
        IDistributedCache cache,
        ILogger<CachedWeatherAdapter> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WeatherSnapshot?> GetLatestAsync(string locationKey, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(locationKey);

        string? cached = null;
        try
        {
            cached = await _cache.GetStringAsync(cacheKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {Key}; falling through to data source.", locationKey);
        }

        if (cached != null)
        {
            try
            {
                return JsonSerializer.Deserialize<WeatherSnapshot>(cached, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached weather for {Key}; falling through.", locationKey);
            }
        }

        var fresh = await _inner.GetLatestAsync(locationKey, ct);
        if (fresh != null)
        {
            try
            {
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(fresh, SerializerOptions),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write weather cache for {Key}.", locationKey);
            }
        }

        return fresh;
    }

    public async Task<WeatherSnapshot> FetchAndStoreAsync(
        string locationKey, double? latitude, double? longitude, CancellationToken ct = default)
    {
        var snapshot = await _inner.FetchAndStoreAsync(locationKey, latitude, longitude, ct);

        try
        {
            await _cache.RemoveAsync(CacheKey(locationKey), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate weather cache for {Key}.", locationKey);
        }

        return snapshot;
    }

    private static string CacheKey(string locationKey) => $"weather:latest:{locationKey}";
}
