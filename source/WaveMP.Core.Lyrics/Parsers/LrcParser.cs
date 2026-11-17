using System.Globalization;
using System.Text.RegularExpressions;
using WaveMP.Core.Lyrics.Documents;

namespace WaveMP.Core.Lyrics.Parsers;

/// <summary>
/// Parses lyrics in the LRC format and plain text. Lines start with one or more timestamps <c>[mm:ss.xx]</c> (also
/// <c>[mm:ss]</c>, <c>[mm:ss.xxx]</c> and <c>[mm:ss:xx]</c>); a line with several timestamps is sung several times.
/// Enhanced LRC adds word timestamps <c>&lt;mm:ss.xx&gt;</c> inside a line; a word timestamp without text at the end of
/// a line marks the end of the last word. ID tags such as <c>[ar:Artist]</c> are skipped, <c>[offset:+500]</c> shifts
/// all times (a positive offset shows the lyrics earlier). A text without any timestamp is static: its lines as they
/// are, with at most one empty line between paragraphs.
/// </summary>
public static partial class LrcParser
{
    public static LyricsDocument Parse(string text)
    {
        TimeSpan offset = TimeSpan.Zero;
        List<(TimeSpan Start, int Order, string Content)> timed = [];
        List<string> plain = [];
        int order = 0;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = Clean(rawLine);
            List<TimeSpan> starts = [];
            int position = 0;
            for (Match match = LineTimestamp().Match(line);
                match.Success && match.Index == position;
                match = match.NextMatch())
            {
                if (ToTime(match) is { } start)
                {
                    starts.Add(start);
                }

                position = match.Index + match.Length;
            }

            if (position > 0)
            {
                string content = line[position..];
                foreach (TimeSpan start in starts)
                {
                    timed.Add((start, order++, content));
                }
            }
            else if (IdTag().Match(line) is { Success: true } tag)
            {
                if (string.Equals(tag.Groups["key"].Value, "offset", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(
                        tag.Groups["value"].Value.Trim(),
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int milliseconds))
                {
                    offset = TimeSpan.FromMilliseconds(milliseconds);
                }
            }
            else
            {
                plain.Add(line.Trim());
            }
        }

        return timed.Count > 0
            ? SynchronizedLyrics.Build(timed.Select(item => ToDraft(item.Start, item.Order, item.Content, offset)))
            : Static(plain);
    }

    /// <summary>
    /// Static lyrics from plain lines: without empty lines at the start and the end and with at most one empty line in
    /// a row.
    /// </summary>
    internal static LyricsDocument Static(IEnumerable<string> lines)
    {
        List<LyricsLine> result = [];
        foreach (string line in lines)
        {
            if (line.Length == 0 && (result.Count == 0 || result[^1].Text.Length == 0))
            {
                continue;
            }

            if (result.Count >= SynchronizedLyrics.MaxLines)
            {
                break;
            }

            result.Add(new LyricsLine(null, null, SynchronizedLyrics.Cut(line), []));
        }

        while (result.Count > 0 && result[^1].Text.Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return new LyricsDocument(LyricsType.Static, result);
    }

    private static DraftLine ToDraft(TimeSpan start, int order, string content, TimeSpan offset)
    {
        TimeSpan lineStart = Shift(start, offset);
        MatchCollection stamps = WordTimestamp().Matches(content);
        if (stamps.Count == 0)
        {
            return new DraftLine(lineStart, order, content.Trim(), []);
        }

        List<DraftWord> words = [];
        TimeSpan? wordsEnd = null;
        string leading = content[..stamps[0].Index];
        if (leading.Trim().Length > 0)
        {
            words.Add(new DraftWord(lineStart, leading));
        }

        for (int index = 0; index < stamps.Count; index++)
        {
            Match stamp = stamps[index];
            int textStart = stamp.Index + stamp.Length;
            int textEnd = index + 1 < stamps.Count ? stamps[index + 1].Index : content.Length;
            if (ToTime(stamp) is not { } time)
            {
                continue;
            }

            string word = content[textStart..textEnd];
            TimeSpan wordStart = Shift(time, offset);
            if (word.Trim().Length > 0)
            {
                words.Add(new DraftWord(wordStart, word));
            }
            else if (index == stamps.Count - 1 && words.Count > 0)
            {
                wordsEnd = wordStart;
            }
        }

        if (words.Count == 0)
        {
            return new DraftLine(lineStart, order, string.Empty, []);
        }

        words[0] = words[0] with { Text = words[0].Text.TrimStart() };
        words[^1] = words[^1] with { Text = words[^1].Text.TrimEnd() };
        string text = string.Concat(words.Select(word => word.Text)).Trim();
        return new DraftLine(lineStart, order, text, words, wordsEnd);
    }

    /// <summary>
    /// The time of a timestamp; the fraction counts tenths, hundredths or thousandths of a second by its digits.
    /// Returns <c>null</c> for seconds of 60 and more.
    /// </summary>
    private static TimeSpan? ToTime(Match match)
    {
        int minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
        int seconds = int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
        string fraction = match.Groups["fraction"].Value;
        int milliseconds = fraction.Length == 0
            ? 0
            : int.Parse(fraction.Length >= 3 ? fraction[..3] : fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return seconds >= 60 ? null : new TimeSpan(0, 0, minutes, seconds, milliseconds);
    }

    /// <summary>
    /// The time shifted by the offset of the file, at least zero.
    /// </summary>
    private static TimeSpan Shift(TimeSpan time, TimeSpan offset)
    {
        TimeSpan shifted = time - offset;
        return shifted < TimeSpan.Zero ? TimeSpan.Zero : shifted;
    }

    /// <summary>
    /// The line without its line break, without a byte order mark and without control characters; tabs become spaces.
    /// </summary>
    private static string Clean(string line)
    {
        string cleaned = line.Replace('\t', ' ').TrimStart('﻿');
        return cleaned.Any(char.IsControl)
            ? new string([.. cleaned.Where(character => !char.IsControl(character))])
            : cleaned;
    }

    [GeneratedRegex(
        @"\s*\[(?<minutes>\d{1,3}):(?<seconds>\d{1,2})(?:[.:](?<fraction>\d{1,6}))?\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex LineTimestamp();

    [GeneratedRegex(
        @"<(?<minutes>\d{1,3}):(?<seconds>\d{1,2})(?:[.:](?<fraction>\d{1,6}))?>",
        RegexOptions.CultureInvariant)]
    private static partial Regex WordTimestamp();

    [GeneratedRegex(@"^\s*\[(?<key>[A-Za-z#]+)\s*:(?<value>[^\]]*)\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdTag();
}
