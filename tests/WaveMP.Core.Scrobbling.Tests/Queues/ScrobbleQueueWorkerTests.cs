using AwesomeAssertions;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Queues;

namespace WaveMP.Core.Scrobbling.Tests.Queues;

[TestFixture]
public class ScrobbleQueueWorkerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly ScrobblingOptions _options = new() { IdleInterval = TimeSpan.FromMinutes(15) };

    [Test]
    public void Wait_NothingQueued_WaitsTheIdleInterval()
    {
        ScrobbleQueueWorker.Wait(null, _now, _options).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Test]
    public void Wait_NextScrobbleDueSoon_WaitsUntilThen()
    {
        ScrobbleQueueWorker.Wait(_now.AddMinutes(2), _now, _options).Should().Be(TimeSpan.FromMinutes(2));
    }

    [Test]
    public void Wait_NextScrobbleDueLate_WaitsAtMostTheIdleInterval()
    {
        ScrobbleQueueWorker.Wait(_now.AddHours(1), _now, _options).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Test]
    public void Wait_NextScrobbleOverdue_WaitsOneSecond()
    {
        ScrobbleQueueWorker.Wait(_now.AddMinutes(-1), _now, _options).Should().Be(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Signal_NotifiedTwice_WakesOnce()
    {
        using ScrobbleQueueSignal signal = new();

        signal.Notify();
        signal.Notify();

        (await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None)).Should().BeTrue();
        (await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None)).Should().BeFalse();
    }
}
