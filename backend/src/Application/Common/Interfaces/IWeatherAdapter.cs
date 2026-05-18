using Backend.Domain.Entities;

namespace Backend.Application.Common.Interfaces;

public interface IWeatherAdapter
{
    Task<WeatherSnapshot> FetchAndStoreAsync(string locationKey, double? latitude, double? longitude, CancellationToken ct = default);
    Task<WeatherSnapshot?> GetLatestAsync(string locationKey, CancellationToken ct = default);
}
