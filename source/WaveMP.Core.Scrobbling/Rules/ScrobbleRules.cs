namespace WaveMP.Core.Scrobbling.Rules;

/// <summary>
/// The usual rules of the scrobbling services: "Now Playing" is reported at the start of a song, the scrobble once
/// half of the song or four minutes have been played, whichever comes first, and songs shorter than 30 seconds are
/// not reported at all.
/// </summary>
public static class ScrobbleRules
{
    /// <summary>
    /// Songs shorter than this are not reported.
    /// </summary>
    public static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A song is scrobbled after four minutes of play at the latest.
    /// </summary>
    public static readonly TimeSpan MaxThreshold = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Whether a song of the duration is reported. A song of unknown duration is reported, since its length cannot be
    /// checked; the clients then scrobble it after four minutes.
    /// </summary>
    public static bool IsEligible(TimeSpan? duration)
    {
        return duration is not { } length || length >= MinDuration;
    }

    /// <summary>
    /// How long a song has to be played before it is scrobbled: half of it or four minutes, whichever comes first;
    /// four minutes if the duration is unknown.
    /// </summary>
    public static TimeSpan Threshold(TimeSpan? duration)
    {
        if (duration is not { } length || length <= TimeSpan.Zero)
        {
            return MaxThreshold;
        }

        TimeSpan half = length / 2;
        return half < MaxThreshold ? half : MaxThreshold;
    }

    /// <summary>
    /// Whether a song played for <paramref name="played"/> counts as a scrobble.
    /// </summary>
    public static bool IsScrobble(TimeSpan? duration, TimeSpan played)
    {
        return IsEligible(duration) && played >= Threshold(duration);
    }

    /// <summary>
    /// Whether a play that started at <paramref name="playedAt"/> is too old for the services at
    /// <paramref name="now"/>.
    /// </summary>
    public static bool IsExpired(DateTimeOffset playedAt, DateTimeOffset now, TimeSpan maxAge)
    {
        return playedAt < now - maxAge;
    }
}
