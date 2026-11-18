using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace WaveMP.Core.Themes.Definitions;

/// <summary>
/// The files of an installed theme. Each file is named after a hash of its content (e.g.
/// <c>3f2a...9c.svg</c>), so its address changes with every change of the file and can be cached forever. The CSS file
/// names the other files with their root-absolute addresses, <c>/api/v1/themes/{id}/files/{name}</c>.
/// </summary>
public static partial class ThemeFiles
{
    /// <summary>
    /// Route of the files of a theme, below the root of the server.
    /// </summary>
    public const string Route = "api/v1/themes/{id}/files/{name}";

    /// <summary>
    /// Pattern of a file name: 32 hexadecimal digits and the extension.
    /// </summary>
    public const string NamePattern = @"^[0-9a-f]{32}\.(css|svg|png|webp)$";

    public const string CssContentType = "text/css; charset=utf-8";
    public const string SvgContentType = "image/svg+xml";
    public const string PngContentType = "image/png";
    public const string WebPContentType = "image/webp";

    /// <summary>
    /// The address of a file of a theme, relative to the root of the server.
    /// </summary>
    public static string AddressOf(string themeId, string fileName)
    {
        return $"/api/v1/themes/{themeId}/files/{fileName}";
    }

    public static bool IsValidName([NotNullWhen(true)] string? name)
    {
        return name is { Length: > 0 } && NameRegex().IsMatch(name);
    }

    /// <summary>
    /// The content type of a file by its extension.
    /// </summary>
    public static string ContentTypeOf(string name)
    {
        return Path.GetExtension(name) switch
        {
            ".css" => CssContentType,
            ".svg" => SvgContentType,
            ".png" => PngContentType,
            _ => WebPContentType,
        };
    }

    [GeneratedRegex(@"\A[0-9a-f]{32}\.(css|svg|png|webp)\z", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}
