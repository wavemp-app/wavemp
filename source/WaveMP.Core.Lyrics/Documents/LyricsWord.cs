namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// A word or syllable of a word-synchronized line. Its text keeps the spaces around it, so the words of a line put
/// together give the text of the line.
/// </summary>
/// <param name="Start">When the word is sung, from the start of the track.</param>
/// <param name="End">When the next word (or the next line) starts, or <c>null</c> for the last word of the lyrics
/// without an end time.</param>
/// <param name="Text">The word.</param>
public sealed record LyricsWord(TimeSpan Start, TimeSpan? End, string Text);
