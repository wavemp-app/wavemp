namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// Simulated time: a delay moves the clock forward by its length at once instead of waiting, and is recorded, so tests
/// check waits and rate limits without waiting for real.
/// </summary>
internal sealed class SimulatedClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<TimeSpan> _delays = [];
    private DateTimeOffset _now = start;

    public SimulatedClock()
        : this(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero))
    {
    }

    /// <summary>
    /// The delays that were waited, in their order.
    /// </summary>
    public IReadOnlyList<TimeSpan> Delays
    {
        get
        {
            lock (_lock)
            {
                return [.. _delays];
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan duration)
    {
        lock (_lock)
        {
            _now += duration;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        SimulatedTimer timer = new();
        if (dueTime == Timeout.InfiniteTimeSpan)
        {
            return timer;
        }

        lock (_lock)
        {
            _delays.Add(dueTime);
            _now += dueTime;
        }

        // Fires after the caller holds the timer, like a real timer.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (!timer.IsDisposed)
            {
                callback(state);
            }
        });
        return timer;
    }

    private sealed class SimulatedTimer : ITimer
    {
        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return !IsDisposed;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
