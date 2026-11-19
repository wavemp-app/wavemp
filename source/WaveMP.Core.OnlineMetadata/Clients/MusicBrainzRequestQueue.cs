using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The queue of the requests to MusicBrainz: one request at a time, in the order they arrive, and at least the request
/// interval of the settings apart (for musicbrainz.org at least one second, as its terms of use ask for). Shared by all
/// searches of all admins.
/// </summary>
internal sealed class MusicBrainzRequestQueue(
    TimeProvider timeProvider,
    ISettingsSnapshot<OnlineMetadataSettings> settings)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _last;

    /// <summary>
    /// Waits until the next request may start; the caller sends it right after.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait = WaitTime(
                _last,
                timeProvider.GetUtcNow(),
                settings.Current.GetMusicBrainzRequestInterval());
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken);
            }

            _last = timeProvider.GetUtcNow();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// How long a request has to wait after the last one started at <paramref name="last"/>.
    /// </summary>
    internal static TimeSpan WaitTime(DateTimeOffset? last, DateTimeOffset now, TimeSpan interval)
    {
        return last is { } previous && previous + interval > now ? previous + interval - now : TimeSpan.Zero;
    }
}
