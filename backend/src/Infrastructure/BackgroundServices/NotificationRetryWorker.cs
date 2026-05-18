using Backend.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.BackgroundServices;

internal sealed class NotificationRetryWorker : PeriodicBackgroundService
{
    private readonly TimeSpan _interval;

    public NotificationRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundWorkerOptions> options,
        ILogger<NotificationRetryWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = options.Value.NotificationRetryInterval;
    }

    protected override string ServiceName => nameof(NotificationRetryWorker);
    protected override TimeSpan Interval => _interval;

    protected override async Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var notifications = scopedServices.GetRequiredService<INotificationService>();
        await notifications.RetryFailedAsync(cancellationToken);
    }
}
