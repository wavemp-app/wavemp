namespace WaveMP.Service.Dtos;

/// <summary>
/// The matches of an album in MusicBrainz, the best first, next to its current tags.
/// </summary>
public sealed class OnlineAlbumMatchesDto
{
    /// <summary>
    /// The current tags of the album in the library.
    /// </summary>
    public required OnlineAlbumValuesDto Current { get; init; }

    /// <summary>
    /// Number of songs of the album.
    /// </summary>
    public required int TrackCount { get; init; }

    /// <summary>
    /// Number of songs whose audio file can be written; the others keep their tags.
    /// </summary>
    public required int EditableTrackCount { get; init; }

    /// <summary>
    /// The matches, the best first (at most 10 by default); empty if MusicBrainz knows none.
    /// </summary>
    public required IReadOnlyList<OnlineAlbumMatchDto> Matches { get; init; }
}
