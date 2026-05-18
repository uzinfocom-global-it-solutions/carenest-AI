using Backend.Domain.Entities;

namespace Backend.Application.Common.Interfaces;

public interface IVoiceActionQueue
{
    ValueTask EnqueueAsync(VoiceAction action, CancellationToken ct = default);
    IAsyncEnumerable<VoiceAction> DequeueAllAsync(CancellationToken ct);
}
