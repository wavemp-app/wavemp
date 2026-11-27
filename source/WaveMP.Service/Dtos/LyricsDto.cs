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
    /// Whether the lyrics belong to another version of the song (its duration differs, e.g. the lyrics of the single
    /// for the longer music video), so they are static: their times would not fit the track.
    /// </summary>
    public required bool FromOtherVersion { get; init; }

    /// <summary>
    /// The lines; synchronized lines are sorted by the time they start.
    /// </summary>
    public required IReadOnlyList<LyricsLineDto> Lines { get; init; }
}
