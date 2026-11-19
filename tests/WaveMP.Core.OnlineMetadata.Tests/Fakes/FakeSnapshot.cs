using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// Settings the test changes at any time.
/// </summary>
internal sealed class FakeSnapshot<T>(T current) : ISettingsSnapshot<T>
    where T : class
{
    public T Current { get; set; } = current;
}
