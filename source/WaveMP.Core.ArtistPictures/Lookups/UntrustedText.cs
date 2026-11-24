using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// Turns texts of online services (e.g. the author of a picture on Wikimedia Commons, which is HTML) into plain text
/// and keeps only absolute https addresses. The results are safe to store and to show as text; they never contain
/// markup.
/// </summary>
internal static partial class UntrustedText
{
    /// <summary>
    /// Longest author text that is kept.
    /// </summary>
    public const int MaxAuthorLength = 500;

    /// <summary>
    /// Longest license name that is kept.
    /// </summary>
    public const int MaxLicenseLength = 200;

    /// <summary>
    /// Longest address that is kept.
    /// </summary>
    public const int MaxUrlLength = 2048;

    /// <summary>
    /// Longest input that is read; more is never a sensible credit.
    /// </summary>
    private const int MaxInputLength = 20_000;

    /// <summary>
    /// The text of HTML without tags, scripts, styles and comments, with decoded entities, without control characters,
    /// with collapsed white space and at most <paramref name="maxLength"/> characters long; <c>null</c> if nothing is
    /// left.
    /// </summary>
    public static string? PlainText(string? html, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        string text;
        try
        {
            text = html.Length > MaxInputLength ? html[..MaxInputLength] : html;
            text = Hidden().Replace(text, " ");
            text = Tag().Replace(text, " ");
            text = WebUtility.HtmlDecode(text);

            // Entities may have encoded markup; it is text now, but no client shall ever get anything like it.
            text = Hidden().Replace(text, " ");
            text = Tag().Replace(text, " ");
            text = text.Replace('<', ' ').Replace('>', ' ');
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        StringBuilder plain = new(text.Length);
        bool space = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character)
                || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format)
            {
                space = plain.Length > 0;
                continue;
            }

            if (space)
            {
                plain.Append(' ');
                space = false;
            }

            plain.Append(character);
        }

        return plain.Length == 0 ? null : Cut(plain.ToString(), maxLength);
    }

    /// <summary>
    /// The address if it is an absolute https address without user info and at most <see cref="MaxUrlLength"/>
    /// characters long; otherwise <c>null</c> (e.g. for <c>javascript:</c>, <c>data:</c> or <c>http:</c>).
    /// </summary>
    public static string? HttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? url)
            || url.Scheme != Uri.UriSchemeHttps
            || url.UserInfo.Length > 0
            || url.Host.Length == 0)
        {
            return null;
        }

        string address = url.AbsoluteUri;
        return address.Length <= MaxUrlLength ? address : null;
    }

    private static string Cut(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        int length = maxLength;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length].TrimEnd();
    }

    [GeneratedRegex(
        @"<!--.*?(-->|$)|<(script|style)\b[^>]*>.*?(</(script|style)\s*>|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Hidden();

    [GeneratedRegex(
        "<[^>]*(>|$)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tag();
}
