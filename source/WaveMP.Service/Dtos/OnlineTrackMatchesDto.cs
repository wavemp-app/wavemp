namespace WaveMP.Service.Dtos;

/// <summary>
/// The matches of a song in MusicBrainz, the best first, next to its current tags.
/// </summary>
public sealed class OnlineTrackMatchesDto
{
    /// <summary>
    /// The current tags of the song and whether they can be written.
    /// </summary>
    public required TrackMetadataDto Current { get; init; }

    /// <summary>
    /// The matches, the best first (at most 10 by default); empty if MusicBrainz knows none.
    /// </summary>
    public required IReadOnlyList<OnlineTrackMatchDto> Matches { get; init; }
}
