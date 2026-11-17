namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// Lyrics as the clients show them: their type and their lines in the order they are sung.
/// </summary>
/// <param name="Type">How the lyrics follow the music.</param>
/// <param name="Lines">The lines; synchronized lines are sorted by the time they start.</param>
public sealed record LyricsDocument(LyricsType Type, IReadOnlyList<LyricsLine> Lines)
{
    /// <summary>
    /// Lyrics without lines.
    /// </summary>
    public static LyricsDocument Empty { get; } = new(LyricsType.Static, []);

    /// <summary>
    /// Whether the lyrics have no line with text.
    /// </summary>
    public bool IsEmpty => Lines.All(line => line.Text.Length == 0);
}
