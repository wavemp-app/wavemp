namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// A line of lyrics. Lines of synchronized lyrics have the time they start; an empty text marks a part without
/// singing.
/// </summary>
/// <param name="Start">When the line starts, from the start of the track; <c>null</c> for static lyrics.</param>
/// <param name="End">When the next line starts; <c>null</c> for the last line and for static lyrics.</param>
/// <param name="Text">The text of the line, without the spaces around it.</param>
/// <param name="Words">The words with their times, for lines with word timestamps; otherwise empty.</param>
public sealed record LyricsLine(TimeSpan? Start, TimeSpan? End, string Text, IReadOnlyList<LyricsWord> Words);
