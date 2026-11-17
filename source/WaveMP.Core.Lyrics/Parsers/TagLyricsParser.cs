using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Metadata.Tags;

namespace WaveMP.Core.Lyrics.Parsers;

/// <summary>
/// Turns the lyrics of the tags of a file into lyrics the clients show. Synchronized lyrics (e.g. ID3 SYLT) come first:
/// every entry is a line, unless the entries carry line breaks; then they are words or syllables, and a line break at
/// the start (or the end) of an entry starts a new line. Unsynchronized lyrics are parsed like an LRC file, because
/// many taggers store LRC text in them; plain text becomes static lyrics.
/// </summary>
public static class TagLyricsParser
{
    /// <summary>
    /// The lyrics of the tags, or an empty document if they have no line with text.
    /// </summary>
    public static LyricsDocument Parse(TagLyrics lyrics)
    {
        LyricsDocument synchronized = ParseSynchronized(lyrics.Synchronized);
        if (!synchronized.IsEmpty)
        {
            return synchronized;
        }

        return lyrics.Unsynchronized is { } text ? LrcParser.Parse(text) : LyricsDocument.Empty;
    }

    internal static LyricsDocument ParseSynchronized(IReadOnlyList<TimedLyricsText> entries)
    {
        if (entries.Count == 0)
        {
            return LyricsDocument.Empty;
        }

        bool hasBreaks = entries.Any(entry => entry.Text.Contains('\n') || entry.Text.Contains('\r'));
        if (!hasBreaks)
        {
            return SynchronizedLyrics.Build(entries.Select((entry, index) => new DraftLine(
                entry.Time,
                index,
                entry.Text.Trim(),
                [])));
        }

        List<DraftLine> lines = [];
        List<DraftWord> words = [];
        foreach (TimedLyricsText entry in entries)
        {
            string text = entry.Text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.StartsWith('\n'))
            {
                Flush(lines, words);
            }

            string word = text.Trim('\n');
            string[] parts = word.Split('\n');
            for (int index = 0; index < parts.Length; index++)
            {
                if (index > 0)
                {
                    Flush(lines, words);
                }

                if (parts[index].Trim().Length > 0)
                {
                    words.Add(new DraftWord(entry.Time, parts[index]));
                }
            }

            if (text.EndsWith('\n') && text.Trim('\n').Length > 0)
            {
                Flush(lines, words);
            }
        }

        Flush(lines, words);
        return SynchronizedLyrics.Build(lines);
    }

    private static void Flush(List<DraftLine> lines, List<DraftWord> words)
    {
        if (words.Count == 0)
        {
            return;
        }

        words[0] = words[0] with { Text = words[0].Text.TrimStart() };
        words[^1] = words[^1] with { Text = words[^1].Text.TrimEnd() };
        string text = string.Concat(words.Select(word => word.Text)).Trim();
        lines.Add(new DraftLine(words[0].Start, lines.Count, text, [.. words]));
        words.Clear();
    }
}
