namespace WaveMP.Service.Dtos;

/// <summary>
/// Where the real song lies in the primary audio file and the primary video of a track.
/// </summary>
public sealed class TrackSongSegmentsDto
{
    /// <summary>
    /// Id of the track.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// The primary audio file; null without one.
    /// </summary>
    public FileSongSegmentDto? Audio { get; init; }

    /// <summary>
    /// The primary video; null without one.
    /// </summary>
    public FileSongSegmentDto? Video { get; init; }
}
