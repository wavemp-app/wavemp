using System.Buffers;
using System.Globalization;
using System.Text;
using WaveMP.Core.Scanning.Discoveries;

namespace WaveMP.Core.Organizing.Templates;

/// <summary>
/// Turns text into a folder or file name that every common file system (Linux, Windows, SMB shares of NAS systems)
/// accepts and that the scan does not skip:
/// <list type="bullet">
/// <item>normalized to NFC, white space joined to single spaces;</item>
/// <item>control characters, lone surrogates and <c>&lt; &gt; : " / \ | ? *</c> replaced by <c>_</c>;</item>
/// <item>separators left at the start (<c>space - , _</c>) and the end (also <c>.</c>) removed, which also drops
/// the trailing dots and spaces Windows and SMB cannot store;</item>
/// <item>a leading dot (a hidden name the scan skips) replaced by <c>_</c>, names the scan ignores (see
/// <see cref="IgnoredNames"/>) prefixed with <c>_</c>, names Windows reserves (<c>CON</c>, <c>NUL</c>, <c>COM1</c> ...,
/// also with an extension) suffixed with <c>_</c>;</item>
/// <item>cut to <see cref="MaxNameBytes"/> UTF-8 bytes on a character boundary.</item>
/// </list>
/// </summary>
public static class PathNameSanitizer
{
    /// <summary>
    /// Longest folder or file name in UTF-8 bytes (the limit of ext4, Btrfs, ZFS and NTFS in practice).
    /// </summary>
    public const int MaxNameBytes = 255;

    public const char Replacement = '_';

    private static readonly SearchValues<char> _invalid = SearchValues.Create("<>:\"/\\|?*");

    private static readonly char[] _leadingSeparators = [' ', '-', ',', '_'];

    private static readonly char[] _trailingSeparators = [' ', '-', ',', '_', '.'];

    private static readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²",
        "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²",
        "LPT³",
    };

    /// <summary>
    /// Replaces the characters that no name may hold, including the folder separators <c>/</c> and <c>\</c>, so a
    /// value such as <c>AC/DC</c> stays one name. White space (also tabs and line breaks) stays; names join it.
    /// </summary>
    public static string ReplaceInvalidCharacters(string text)
    {
        StringBuilder builder = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[index + 1]);
                index++;
            }
            else if (char.IsSurrogate(character)
                || (char.IsControl(character) && !char.IsWhiteSpace(character))
                || _invalid.Contains(character))
            {
                builder.Append(Replacement);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The name as described above; empty if nothing is left.
    /// </summary>
    /// <param name="name">The text of the name.</param>
    /// <param name="reservedBytes">UTF-8 bytes the name must leave free, e.g. for <c>.flac</c> after a base
    /// name.</param>
    public static string SanitizeName(string name, int reservedBytes = 0)
    {
        // Lone surrogates go first: normalizing a text with them fails.
        string text = JoinWhiteSpace(ReplaceInvalidCharacters(name)).Normalize(NormalizationForm.FormC);
        text = text.TrimStart(_leadingSeparators).TrimEnd(_trailingSeparators);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text[0] == '.')
        {
            text = Replacement + text[1..];
        }

        if (IgnoredNames.IsIgnored(text))
        {
            text = Replacement + text;
        }

        // The suffix of a reserved name comes last, so no trimming removes it, and within the limit.
        int maxBytes = Math.Max(1, MaxNameBytes - reservedBytes);
        text = Cut(text, maxBytes).TrimEnd(_trailingSeparators);
        return IsReservedName(text)
            ? AvoidReservedName(Cut(text, maxBytes - 1).TrimEnd(_trailingSeparators))
            : text;
    }

    /// <summary>
    /// Whether Windows reserves the name for a device: the part before the first dot, without trailing spaces, is
    /// <c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>, <c>COM0</c> to <c>COM9</c> or <c>LPT0</c> to <c>LPT9</c>.
    /// </summary>
    public static bool IsReservedName(string name)
    {
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = (dot < 0 ? name : name[..dot]).TrimEnd(' ');
        return _reserved.Contains(stem);
    }

    /// <summary>
    /// The length of a text in UTF-8 bytes.
    /// </summary>
    public static int ByteCountOf(string text)
    {
        return Encoding.UTF8.GetByteCount(text);
    }

    private static string AvoidReservedName(string text)
    {
        if (!IsReservedName(text))
        {
            return text;
        }

        int dot = text.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? text + Replacement : text[..dot].TrimEnd(' ') + Replacement + text[dot..];
    }

    private static string JoinWhiteSpace(string text)
    {
        StringBuilder builder = new(text.Length);
        bool space = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string Cut(string text, int maxBytes)
    {
        if (ByteCountOf(text) <= maxBytes)
        {
            return text;
        }

        StringBuilder builder = new();
        int bytes = 0;
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            int count = ByteCountOf(element);
            if (bytes + count > maxBytes)
            {
                break;
            }

            builder.Append(element);
            bytes += count;
        }

        return builder.ToString();
    }
}
