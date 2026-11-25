namespace WaveMP.Core.Devices.Tests.Fakes;

/// <summary>
/// Time the test moves forward.
/// </summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan duration)
    {
        _now += duration;
    }
}
