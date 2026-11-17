namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// An entry of synchronized lyrics in a tag: a line, a word or a syllable with the time it starts. The text is kept
/// as stored, with its spaces and line breaks; a line break at its start usually starts a new line.
/// </summary>
/// <param name="Time">Start of the text from the start of the track.</param>
/// <param name="Text">The text.</param>
public sealed record TimedLyricsText(TimeSpan Time, string Text);
