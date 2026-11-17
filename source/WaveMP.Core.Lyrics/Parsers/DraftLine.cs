namespace WaveMP.Core.Lyrics.Parsers;

/// <summary>
/// A synchronized line while it is parsed, before the times of the following lines are known.
/// </summary>
/// <param name="Start">When the line starts.</param>
/// <param name="Order">Position in the source, so lines with the same time keep their order.</param>
/// <param name="Text">The text of the line, without the spaces around it.</param>
/// <param name="Words">The words with their times; empty for lines without word timestamps.</param>
/// <param name="WordsEnd">When the last word ends, if the source says so (a timestamp without text at the end of an
/// enhanced LRC line).</param>
internal sealed record DraftLine(
    TimeSpan Start,
    int Order,
    string Text,
    IReadOnlyList<DraftWord> Words,
    TimeSpan? WordsEnd = null);
