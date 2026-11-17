namespace WaveMP.Core.Lyrics.Parsers;

/// <summary>
/// A word of a synchronized line while it is parsed.
/// </summary>
/// <param name="Start">When the word starts.</param>
/// <param name="Text">The word with the spaces around it.</param>
internal sealed record DraftWord(TimeSpan Start, string Text);
