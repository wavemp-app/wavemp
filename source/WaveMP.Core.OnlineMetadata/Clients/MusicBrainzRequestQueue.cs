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
    /// Waits until the next request may start and returns its turn. The caller sends the request while it holds the
    /// turn and disposes it once the request is sent, so the next request cannot start before this one has left and
    /// the interval counts from the real start of each request.
    /// </summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
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
            return new Turn(_gate);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    /// <summary>
    /// How long a request has to wait after the last one started at <paramref name="last"/>.
    /// </summary>
    internal static TimeSpan WaitTime(DateTimeOffset? last, DateTimeOffset now, TimeSpan interval)
    {
        return last is { } previous && previous + interval > now ? previous + interval - now : TimeSpan.Zero;
    }

    /// <summary>
    /// The turn of one request; disposing it lets the next request in, once.
    /// </summary>
    private sealed class Turn(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
