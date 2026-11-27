using System.Globalization;
using System.Text;

namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// Writes lyrics as text: plain lines without times, or synchronized lines in the LRC format (one timestamp per line,
/// as LRCLIB keeps them), and cleans texts that admins upload.
/// </summary>
public static class LyricsTexts
{
    /// <summary>
    /// The lines of the lyrics without times, one per line. Parts without singing become a single empty line; there
    /// are no empty lines at the start and the end.
    /// </summary>
    public static string ToPlain(LyricsDocument document)
    {
        StringBuilder builder = new();
        bool gap = false;
        foreach (LyricsLine line in document.Lines)
        {
            if (line.Text.Length == 0)
            {
                gap = builder.Length > 0;
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
                if (gap)
                {
                    builder.Append('\n');
                }
            }

            builder.Append(line.Text);
            gap = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// The lines of synchronized lyrics in the LRC format with one timestamp per line (<c>[01:02.50]Text</c>), also
    /// for word-synchronized lyrics; <c>null</c> for static lyrics.
    /// </summary>
    public static string? ToSynchronized(LyricsDocument document)
    {
        if (document.Type == LyricsType.Static)
        {
            return null;
        }

        StringBuilder builder = new();
        foreach (LyricsLine line in document.Lines)
        {
            if (line.Start is not { } start)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append('[').Append(Timestamp(start)).Append(']').Append(line.Text);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// An uploaded text without byte order mark, with <c>\n</c> as line break and without empty lines at the start and
    /// the end.
    /// </summary>
    public static string Clean(string text)
    {
        return text.TrimStart('\uFEFF').ReplaceLineEndings("\n").Trim('\n', '\r', ' ', '\t');
    }

    private static string Timestamp(TimeSpan time)
    {
        long centiseconds = (long)Math.Round(time.TotalMilliseconds / 10, MidpointRounding.AwayFromZero);
        long minutes = centiseconds / 6000;
        long seconds = centiseconds / 100 % 60;
        long rest = centiseconds % 100;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{seconds:00}.{rest:00}");
    }
}
