using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class SseConnectionManager : ISseConnectionManager
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<SseEvent>>> _connections = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<SseEvent> SubscribeAsync(
        string userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var bag = _connections.GetOrAdd(userId, _ => new ConcurrentBag<Channel<SseEvent>>());
        bag.Add(channel);
        _logger.LogDebug("SSE client connected for user {UserId}", userId);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
                yield return evt;
        }
        finally
        {
            var remaining = bag.Where(c => !ReferenceEquals(c, channel)).ToList();
            if (remaining.Count == 0)
                _connections.TryRemove(userId, out _);
            else
            {
                var newBag = new ConcurrentBag<Channel<SseEvent>>(remaining);
                _connections[userId] = newBag;
            }
            _logger.LogDebug("SSE client disconnected for user {UserId}", userId);
        }
    }

    public async Task PublishAsync(string userId, SseEvent evt, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(userId, out var bag)) return;

        foreach (var channel in bag)
        {
            try { await channel.Writer.WriteAsync(evt, ct); }
            catch (ChannelClosedException) { }
        }
    }

    public async Task PublishToFamilyAsync(int familyId, SseEvent evt, CancellationToken ct = default)
    {
        await Task.CompletedTask;
    }

    public int GetConnectionCount(string userId) =>
        _connections.TryGetValue(userId, out var bag) ? bag.Count : 0;
}
