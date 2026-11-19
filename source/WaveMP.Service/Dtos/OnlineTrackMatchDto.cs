namespace WaveMP.Service.Dtos;

/// <summary>
/// A recording of MusicBrainz on one of its releases that may be the song, with the tags it suggests.
/// </summary>
public sealed class OnlineTrackMatchDto
{
    /// <summary>
    /// The MusicBrainz id (MBID) of the recording.
    /// </summary>
    public required Guid RecordingId { get; init; }

    /// <summary>
    /// The release; null for a recording without release (no album, no cover).
    /// </summary>
    public OnlineReleaseDto? Release { get; init; }

    /// <summary>
    /// How well the match agrees with the song, 0 to 100: title, artist, album and duration compared with the song.
    /// </summary>
    public required int Agreement { get; init; }

    /// <summary>
    /// Length of the recording in milliseconds, if known.
    /// </summary>
    public long? DurationMilliseconds { get; init; }

    public string? Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    public string? AlbumArtist { get; init; }

    public int? Year { get; init; }

    public int? TrackNumber { get; init; }

    public int? DiscNumber { get; init; }

    public string? Genre { get; init; }
}
