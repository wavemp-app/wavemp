using System.Text;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// Checks the CSS file of an uploaded theme and rewrites its addresses. A theme only uses its own files: every
/// address is a path relative to the CSS file that names a picture or a font of the package (in any spelling a browser
/// accepts: <c>url(a.svg)</c>, <c>url("a.svg")</c>, escapes, <c>src()</c>, <c>image-set()</c>). External addresses (any scheme,
/// including <c>data:</c>, and <c>//</c>), root-absolute paths, <c>@import</c>, <c>@layer</c> and at-rules outside the
/// list below are rejected. Each address is replaced by <c>url("...")</c> with the root-absolute address of the
/// installed file, so it also works where the theme puts it into a CSS variable that the base styles use (a relative
/// address there would be resolved against the base styles).
/// </summary>
internal static class ThemeStylesheet
{
    /// <summary>
    /// Most errors reported per CSS file.
    /// </summary>
    public const int MaxErrors = 20;

    // At-rules a theme can use; everything else, including @import, @namespace and @layer, is rejected.
    private static readonly HashSet<string> _allowedAtRules = new(StringComparer.Ordinal)
    {
        "charset",
        "media",
        "supports",
        "container",
        "keyframes",
        "-webkit-keyframes",
        "font-face",
        "property",
        "starting-style",
        "scope",
        "counter-style",
        "font-feature-values",
        "font-palette-values",
        "view-transition",
        "page",
        // Blocks inside @font-feature-values.
        "swash",
        "annotation",
        "ornaments",
        "stylistic",
        "styleset",
        "character-variant",
        "historical-forms",
    };

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Checks the CSS file at <paramref name="path"/> and returns it with rewritten addresses, or <c>null</c> with the
    /// reasons in <paramref name="errors"/>. <paramref name="addressOf"/> returns the public address of a picture or a
    /// font of the package by its path, or <c>null</c> if the package has no such file at that path.
    /// <paramref name="installedFileOf"/> is only given when the CSS file of an installed theme is checked again: its
    /// addresses are already rewritten, so it returns the file that a root-absolute address of the theme names, or
    /// <c>null</c> for any other root-absolute address (which is rejected as usual).
    /// </summary>
    public static string? Check(
        string path,
        byte[] content,
        Func<string, string?> addressOf,
        List<ThemeError> errors,
        Func<string, string?>? installedFileOf = null)
    {
        string? text = Decode(content);
        if (text is null)
        {
            errors.Add(new ThemeError(ThemeErrorCode.StylesheetInvalidEncoding, path));
            return null;
        }

        var scan = CssScan.Run(text);
        List<ThemeError> found = [];
        foreach (int line in scan.SyntaxErrors)
        {
            found.Add(new ThemeError(ThemeErrorCode.StylesheetSyntaxError, path, line));
        }

        foreach (int line in scan.IndirectAddresses)
        {
            found.Add(new ThemeError(ThemeErrorCode.StylesheetInvalidUrl, path, line));
        }

        foreach (CssAtRule rule in scan.AtRules.Where(rule => !_allowedAtRules.Contains(rule.Name)))
        {
            ThemeErrorCode code = rule.Name switch
            {
                "import" => ThemeErrorCode.StylesheetImport,
                "layer" => ThemeErrorCode.StylesheetLayer,
                _ => ThemeErrorCode.StylesheetAtRuleNotAllowed,
            };
            found.Add(new ThemeError(code, path, rule.Line));
        }

        StringBuilder result = new(scan.Text.Length);
        int copied = 0;
        foreach (CssReference reference in scan.References)
        {
            string? replacement = Rewrite(path, reference, addressOf, installedFileOf, found);
            if (replacement is null)
            {
                continue;
            }

            result.Append(scan.Text, copied, reference.Start - copied).Append(replacement);
            copied = reference.End;
        }

        if (found.Count > 0)
        {
            errors.AddRange(found.OrderBy(error => error.Line).Take(MaxErrors));
            return null;
        }

        return result.Append(scan.Text, copied, scan.Text.Length - copied).ToString();
    }

    private static string? Rewrite(
        string path,
        CssReference reference,
        Func<string, string?> addressOf,
        Func<string, string?>? installedFileOf,
        List<ThemeError> errors)
    {
        CssAddress address = CssAddresses.Classify(reference.Value);
        switch (address.Kind)
        {
            case CssAddressKind.External:
                errors.Add(new ThemeError(ThemeErrorCode.StylesheetExternalUrl, path, reference.Line));
                return null;
            case CssAddressKind.Absolute:
                if (address.Path.Length > 0 && installedFileOf?.Invoke(address.Path) is { } installed)
                {
                    return Published(installed);
                }

                errors.Add(new ThemeError(ThemeErrorCode.StylesheetAbsoluteUrl, path, reference.Line));
                return null;
            case CssAddressKind.Fragment:
                return Format(address.Fragment, reference.IsString);
            case CssAddressKind.Relative:
                string? target = Packages.PackagePaths.Resolve(path, address.Path);
                if (target is null)
                {
                    errors.Add(new ThemeError(ThemeErrorCode.StylesheetInvalidUrl, path, reference.Line));
                    return null;
                }

                return Published(target);
            default:
                errors.Add(new ThemeError(ThemeErrorCode.StylesheetInvalidUrl, path, reference.Line));
                return null;
        }

        string? Published(string target)
        {
            if (addressOf(target) is not { } published)
            {
                errors.Add(new ThemeError(ThemeErrorCode.StylesheetMissingFile, path, reference.Line, File: target));
                return null;
            }

            return Format(published + address.Fragment, reference.IsString);
        }
    }

    /// <summary>
    /// The new text of an address. The addresses consist of letters, digits and <c>/ . _ - # :</c> only, so they need
    /// no escapes.
    /// </summary>
    private static string Format(string address, bool isString)
    {
        return isString ? $"\"{address}\"" : $"url(\"{address}\")";
    }

    /// <summary>
    /// The text of a CSS file in UTF-8 (with or without byte order mark); <c>null</c> for other encodings and broken
    /// UTF-8.
    /// </summary>
    internal static string? Decode(byte[] content)
    {
        ReadOnlySpan<byte> bytes = content;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            bytes = bytes[3..];
        }

        try
        {
            return _strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
