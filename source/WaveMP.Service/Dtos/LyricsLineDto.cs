namespace WaveMP.Service.Dtos;

/// <summary>
/// A line of lyrics. An empty text marks a part without singing.
/// </summary>
public sealed class LyricsLineDto
{
    /// <summary>
    /// When the line starts, in milliseconds from the start of the track; null for static lyrics.
    /// </summary>
    public required long? StartMilliseconds { get; init; }

    /// <summary>
    /// When the next line starts, in milliseconds; null for the last line and for static lyrics.
    /// </summary>
    public required long? EndMilliseconds { get; init; }

    public required string Text { get; init; }

    /// <summary>
    /// The words with their times, for lines with word timestamps; otherwise empty.
    /// </summary>
    public required IReadOnlyList<LyricsWordDto> Words { get; init; }
}
