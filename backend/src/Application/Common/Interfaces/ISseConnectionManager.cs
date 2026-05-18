namespace Backend.Application.Common.Interfaces;

public interface ISseConnectionManager
{
    IAsyncEnumerable<SseEvent> SubscribeAsync(string userId, CancellationToken ct);
    Task PublishAsync(string userId, SseEvent evt, CancellationToken ct = default);
    Task PublishToFamilyAsync(int familyId, SseEvent evt, CancellationToken ct = default);
    int GetConnectionCount(string userId);
}

public record SseEvent(string EventType, string Payload, string? Id = null);
