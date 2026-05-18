using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Backend.Infrastructure.Caching;

internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _multiplexer;

    public RedisHealthCheck(IConnectionMultiplexer multiplexer) => _multiplexer = multiplexer;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await _multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy(
                $"Redis ping ok ({latency.TotalMilliseconds:F1} ms)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed", ex);
        }
    }
}
