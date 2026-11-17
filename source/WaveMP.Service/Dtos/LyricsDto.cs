namespace WaveMP.Service.Dtos;

/// <summary>
/// The lyrics of a track.
/// </summary>
public sealed class LyricsDto
{
    /// <summary>
    /// How the lyrics follow the music.
    /// </summary>
    public required LyricsTypeDto Type { get; init; }

    /// <summary>
    /// Where the lyrics come from.
    /// </summary>
    public required LyricsSourceDto Source { get; init; }

    /// <summary>
    /// The lines; synchronized lines are sorted by the time they start.
    /// </summary>
    public required IReadOnlyList<LyricsLineDto> Lines { get; init; }
}
