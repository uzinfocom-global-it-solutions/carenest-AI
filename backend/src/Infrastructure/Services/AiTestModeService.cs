namespace Backend.Infrastructure.Services;

public sealed class AiTestModeService
{
    private volatile bool _enabled;
    private volatile int  _intervalSeconds = 5;

    private long _totalEvents;
    private long _totalPush;
    private long _totalSse;
    private long _totalVoice;
    private long _startedAt;

    private readonly List<TestModeEvent> _recent = [];
    private readonly List<string> _reasoningStream = [];
    private readonly object _lock = new();

    public bool IsEnabled      => _enabled;
    public int IntervalSeconds => _intervalSeconds;

    public void Enable(int? intervalSeconds = null)
    {
        _intervalSeconds = Math.Clamp(intervalSeconds ?? 5, 3, 60);
        if (!_enabled)
        {
            Interlocked.Exchange(ref _startedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _enabled = true;
        }
    }

    public void Disable() => _enabled = false;

    public void IncrementPush(long n)    => Interlocked.Add(ref _totalPush, n);
    public void IncrementSse(long n = 1) => Interlocked.Add(ref _totalSse, n);
    public void IncrementVoice()         => Interlocked.Increment(ref _totalVoice);

    public void AddEvent(TestModeEvent evt)
    {
        Interlocked.Increment(ref _totalEvents);
        lock (_lock)
        {
            _recent.Insert(0, evt);
            if (_recent.Count > 50) _recent.RemoveAt(_recent.Count - 1);
        }
    }

    public void AddReasoning(string line)
    {
        lock (_lock)
        {
            _reasoningStream.Insert(0, $"[{DateTime.UtcNow:HH:mm:ss}] {line}");
            if (_reasoningStream.Count > 100) _reasoningStream.RemoveAt(_reasoningStream.Count - 1);
        }
    }

    public TestModeStats GetStats()
    {
        lock (_lock)
        {
            var uptime = _startedAt > 0
                ? (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _startedAt)
                : 0L;

            var notifPerMin = uptime > 0
                ? Math.Round(_totalEvents * 60.0 / uptime, 1)
                : 0.0;

            return new TestModeStats(
                _enabled, _intervalSeconds,
                _totalEvents, _totalPush, _totalSse, _totalVoice,
                notifPerMin,
                [.. _recent.Take(20)],
                [.. _reasoningStream.Take(30)]);
        }
    }
}

public sealed record TestModeEvent(
    DateTimeOffset Timestamp,
    int FamilyId,
    string ChainId,
    int Executed,
    int PushSent,
    int SseSent,
    string Summary);

public sealed record TestModeStats(
    bool IsEnabled,
    int IntervalSeconds,
    long TotalEvents,
    long TotalPush,
    long TotalSse,
    long TotalVoice,
    double NotificationsPerMinute,
    List<TestModeEvent> RecentEvents,
    List<string> ReasoningStream);
