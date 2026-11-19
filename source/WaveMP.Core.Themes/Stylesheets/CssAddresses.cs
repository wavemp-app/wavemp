using System.Text.RegularExpressions;

namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// Sorts an address of a style sheet the way a browser would read it. Browsers drop tabs and line breaks from
/// addresses, read a backslash as a slash, and treat <c>%2e%2e</c> as <c>..</c>; such addresses never count as
/// relative paths here, so no spelling can turn an address into an external one.
/// </summary>
internal static partial class CssAddresses
{
    /// <summary>
    /// The kind of an address with its path and fragment.
    /// </summary>
    public static CssAddress Classify(string value)
    {
        string address = value.Trim(' ');
        if (address.Length == 0 || address.Any(character => character < ' ' || character == '\u007F'))
        {
            return new CssAddress(CssAddressKind.Invalid);
        }

        if (address.Contains('\\', StringComparison.Ordinal) || address.StartsWith("//", StringComparison.Ordinal))
        {
            return new CssAddress(CssAddressKind.External);
        }

        if (SchemePattern().IsMatch(address))
        {
            return new CssAddress(CssAddressKind.External);
        }

        if (address.StartsWith('#'))
        {
            return FragmentPattern().IsMatch(address)
                ? new CssAddress(CssAddressKind.Fragment, Fragment: address)
                : new CssAddress(CssAddressKind.Invalid);
        }

        int hash = address.IndexOf('#', StringComparison.Ordinal);
        string path = hash < 0 ? address : address[..hash];
        string fragment = hash < 0 ? string.Empty : address[hash..];
        bool isValid = !path.Contains('?', StringComparison.Ordinal) &&
            (fragment.Length == 0 || FragmentPattern().IsMatch(fragment));
        if (address.StartsWith('/'))
        {
            // The path only counts when it is a plain one, e.g. for the addresses of an installed theme.
            return isValid
                ? new CssAddress(CssAddressKind.Absolute, path, fragment)
                : new CssAddress(CssAddressKind.Absolute);
        }

        return isValid
            ? new CssAddress(CssAddressKind.Relative, path, fragment)
            : new CssAddress(CssAddressKind.Invalid);
    }

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemePattern();

    [GeneratedRegex(@"\A#[A-Za-z0-9_\-.:]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex FragmentPattern();
}

internal enum CssAddressKind
{
    /// <summary>
    /// Empty, with control characters, a query or a strange fragment.
    /// </summary>
    Invalid,

    /// <summary>
    /// With a scheme (<c>https:</c>, <c>data:</c>, <c>javascript:</c> ...), <c>//</c> or a backslash.
    /// </summary>
    External,

    /// <summary>
    /// Starts with <c>/</c>.
    /// </summary>
    Absolute,

    /// <summary>
    /// Only a fragment such as <c>#glow</c>: an element of the same document, nothing is loaded.
    /// </summary>
    Fragment,

    /// <summary>
    /// A relative path, maybe with a fragment; whether it names a file of the package is checked separately.
    /// </summary>
    Relative,
}

internal readonly record struct CssAddress(CssAddressKind Kind, string Path = "", string Fragment = "");
