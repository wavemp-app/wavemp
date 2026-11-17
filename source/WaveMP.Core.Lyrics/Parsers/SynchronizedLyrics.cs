using WaveMP.Core.Lyrics.Documents;

namespace WaveMP.Core.Lyrics.Parsers;

/// <summary>
/// Completes parsed synchronized lines: sorts them by time, gives every line and word its end (the start of the next
/// line or word) and decides whether the lyrics are word-synchronized.
/// </summary>
internal static class SynchronizedLyrics
{
    /// <summary>
    /// Most lines kept; longer lyrics are cut, so a damaged file cannot produce huge answers.
    /// </summary>
    public const int MaxLines = 5000;

    /// <summary>
    /// Most characters of a line or word; longer texts are cut.
    /// </summary>
    public const int MaxTextLength = 1000;

    public static LyricsDocument Build(IEnumerable<DraftLine> drafts)
    {
        DraftLine[] ordered = [.. drafts
            .OrderBy(draft => draft.Start)
            .ThenBy(draft => draft.Order)
            .Take(MaxLines)];

        // Lyrics whose lines have one timed word at most are line-synchronized lyrics written in the word format.
        bool hasWords = ordered.Any(draft => draft.Words.Count > 1);
        List<LyricsLine> lines = new(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            DraftLine draft = ordered[index];
            TimeSpan? next = index + 1 < ordered.Length ? ordered[index + 1].Start : null;
            IReadOnlyList<LyricsWord> words = hasWords && draft.Words.Count > 0 ? Words(draft, next) : [];
            lines.Add(new LyricsLine(draft.Start, next, Cut(draft.Text), words));
        }

        return new LyricsDocument(hasWords ? LyricsType.WordSynchronized : LyricsType.LineSynchronized, lines);
    }

    /// <summary>
    /// Cuts a text to <see cref="MaxTextLength"/> characters.
    /// </summary>
    public static string Cut(string text)
    {
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    private static LyricsWord[] Words(DraftLine draft, TimeSpan? nextLine)
    {
        IReadOnlyList<DraftWord> words = draft.Words;
        var result = new LyricsWord[words.Count];
        for (int index = 0; index < words.Count; index++)
        {
            TimeSpan start = words[index].Start;
            TimeSpan? end = index + 1 < words.Count ? words[index + 1].Start : draft.WordsEnd ?? nextLine;
            if (end < start)
            {
                end = start;
            }

            result[index] = new LyricsWord(start, end, Cut(words[index].Text));
        }

        return result;
    }
}
