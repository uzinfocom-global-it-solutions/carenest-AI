using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Services;

public sealed class VoiceActionQueue : IVoiceActionQueue
{
    private readonly Channel<VoiceAction> _channel = Channel.CreateBounded<VoiceAction>(
        new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(VoiceAction action, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(action, ct);

    public async IAsyncEnumerable<VoiceAction> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var action in _channel.Reader.ReadAllAsync(ct))
            yield return action;
    }
}
