namespace Backend.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(string userId, string action, string entityType, int? entityId, object? metadata = null, CancellationToken ct = default);
}
