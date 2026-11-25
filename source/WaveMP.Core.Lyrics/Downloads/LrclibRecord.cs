namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// A track of LRCLIB with its lyrics, as the API returns it.
/// </summary>
public sealed record LrclibRecord
{
    public long Id { get; init; }

    public string? TrackName { get; init; }

    public string? ArtistName { get; init; }

    public string? AlbumName { get; init; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Whether the track has no lyrics because it is instrumental.
    /// </summary>
    public bool Instrumental { get; init; }

    /// <summary>
    /// The lyrics without timestamps.
    /// </summary>
    public string? PlainLyrics { get; init; }

    /// <summary>
    /// The lyrics in the LRC format, with a timestamp per line.
    /// </summary>
    public string? SyncedLyrics { get; init; }

    public bool HasSyncedLyrics => !string.IsNullOrWhiteSpace(SyncedLyrics);

    public bool HasLyrics => HasSyncedLyrics || !string.IsNullOrWhiteSpace(PlainLyrics);

    /// <summary>
    /// The lyrics to keep: the synchronized ones if there are any, otherwise the plain ones.
    /// </summary>
    public string? Lyrics => HasSyncedLyrics
        ? SyncedLyrics
        : string.IsNullOrWhiteSpace(PlainLyrics) ? null : PlainLyrics;
}
