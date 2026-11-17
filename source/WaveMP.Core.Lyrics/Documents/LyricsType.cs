namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// How the lyrics follow the music.
/// </summary>
public enum LyricsType
{
    /// <summary>
    /// Every line has the time it starts.
    /// </summary>
    LineSynchronized,

    /// <summary>
    /// Every line has the time it starts, and the words of at least one line have the times they start (enhanced
    /// LRC, word or syllable timestamps).
    /// </summary>
    WordSynchronized,

    /// <summary>
    /// Plain lines without times.
    /// </summary>
    Static,
}
