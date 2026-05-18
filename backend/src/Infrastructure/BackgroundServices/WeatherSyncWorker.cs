using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class WeatherSyncWorker : PeriodicBackgroundService
{
    private readonly TimeSpan _interval;

    public WeatherSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<WeatherSyncWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = options.Value.WeatherSyncInterval;
    }

    protected override string ServiceName => nameof(WeatherSyncWorker);
    protected override TimeSpan Interval => _interval;

    protected override async Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var context = scopedServices.GetRequiredService<ApplicationDbContext>();
        var weather = scopedServices.GetRequiredService<IWeatherAdapter>();
        var sse     = scopedServices.GetRequiredService<ISseConnectionManager>();
        var logger  = scopedServices.GetRequiredService<ILogger<WeatherSyncWorker>>();

        var families = await context.Families
            .Where(f => f.DefaultLocationKey != null)
            .Select(f => new
            {
                f.Id,
                f.DefaultLocationKey,
                f.DefaultLatitude,
                f.DefaultLongitude,
                MemberIds = f.Members
                    .Where(m => m.Status == FamilyMemberStatusEnum.Active)
                    .Select(m => m.UserId)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        logger.LogDebug("[WeatherSync] Starting — {Count} families to sync", families.Count);

        foreach (var family in families)
        {
            try
            {
                logger.LogDebug("[WeatherSync] Fetching family={FamilyId} key={Key} lat={Lat} lon={Lon}",
                    family.Id, family.DefaultLocationKey, family.DefaultLatitude, family.DefaultLongitude);

                var snapshot = await weather.FetchAndStoreAsync(
                    family.DefaultLocationKey!,
                    family.DefaultLatitude,
                    family.DefaultLongitude,
                    cancellationToken);

                logger.LogInformation("[WeatherSync] Stored snapshot for family={FamilyId}: {Temp}°C {Condition}",
                    family.Id, snapshot.TemperatureC, snapshot.WeatherCondition);

                var ssePayload = JsonSerializer.Serialize(new
                {
                    familyId    = family.Id,
                    locationKey = snapshot.LocationKey,
                    latitude    = snapshot.Latitude,
                    longitude   = snapshot.Longitude,
                    temperatureC = snapshot.TemperatureC,
                    condition   = snapshot.WeatherCondition.ToString(),
                    collectedAt = snapshot.CollectedAt,
                });
                var sseEvent = new SseEvent("weather_updated", ssePayload);

                foreach (var uid in family.MemberIds)
                {
                    try { await sse.PublishAsync(uid, sseEvent, cancellationToken); }
                    catch { /* SSE failure must never abort weather sync */ }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WeatherSync] Failed for family={FamilyId} key={Key}",
                    family.Id, family.DefaultLocationKey);
            }
        }

        logger.LogDebug("[WeatherSync] Iteration complete: {Count} families", families.Count);
    }
}
