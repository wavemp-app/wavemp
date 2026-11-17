namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// What LRCLIB is asked for: the title of the track, its artist, album and duration as far as known.
/// </summary>
public sealed record LrclibQuery(string TrackName, string? ArtistName, string? AlbumName, TimeSpan? Duration)
{
    /// <summary>
    /// Whether the query names everything the exact lookup of LRCLIB needs.
    /// </summary>
    public bool IsComplete => ArtistName is not null && AlbumName is not null && Duration is not null;
}
