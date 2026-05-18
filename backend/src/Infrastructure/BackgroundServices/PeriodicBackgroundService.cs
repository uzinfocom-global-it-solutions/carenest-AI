using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected PeriodicBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected abstract string ServiceName { get; }
    protected abstract TimeSpan Interval { get; }

    protected abstract Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Service} started, interval={Interval}", ServiceName, Interval);

        // Fire once immediately so the first notification doesn't wait a full interval
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await ExecuteIterationAsync(scope.ServiceProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception ex) { _logger.LogError(ex, "{Service} initial iteration failed", ServiceName); }

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await ExecuteIterationAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Service} iteration failed", ServiceName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        _logger.LogInformation("{Service} stopped", ServiceName);
    }
}
