namespace WaveMP.Service.Dtos;

/// <summary>
/// Corrects where the song lies in a file of a track. A value left out (null) comes from the analysis or the file.
/// </summary>
public sealed class UpdateTrackSongSegmentDto
{
    /// <summary>
    /// Position of the file where the song starts, in milliseconds from -600000 to the end of the file (exclusive);
    /// negative if the file starts that far into the song.
    /// </summary>
    public int? StartMilliseconds { get; init; }

    /// <summary>
    /// Position of the file where the song ends, in milliseconds, after the start and at most at the end of the file.
    /// </summary>
    public int? EndMilliseconds { get; init; }
}
