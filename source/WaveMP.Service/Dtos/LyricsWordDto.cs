namespace WaveMP.Service.Dtos;

/// <summary>
/// A word or syllable of a word-synchronized line. Its text keeps the spaces around it, so the words of a line put
/// together give the text of the line.
/// </summary>
public sealed class LyricsWordDto
{
    /// <summary>
    /// When the word is sung, in milliseconds from the start of the track.
    /// </summary>
    public required long StartMilliseconds { get; init; }

    /// <summary>
    /// When the next word or line starts, in milliseconds; null for the last word without an end.
    /// </summary>
    public required long? EndMilliseconds { get; init; }

    public required string Text { get; init; }
}
