namespace Backend.Application.Common.Interfaces;

public interface INotificationService
{
    Task DispatchAsync(int recommendationId, CancellationToken ct = default);
    Task RetryFailedAsync(CancellationToken ct = default);
}
