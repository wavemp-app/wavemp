using Microsoft.Extensions.Options;
using WaveMP.Core.Lyrics.Options;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Spaces the requests to LRCLIB: one at a time, at least <see cref="LyricsOptions.RequestInterval"/> apart, for the
/// download job and the downloads of admins together.
/// </summary>
internal sealed class LrclibRequestPacer(TimeProvider timeProvider, IOptions<LyricsOptions> options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _last;

    /// <summary>
    /// Waits until the next request may start.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait = WaitTime(_last, timeProvider.GetUtcNow(), options.Value.RequestInterval);
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
