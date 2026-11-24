using System.Globalization;
using System.Text;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Organizing.Templates;

/// <summary>
/// Puts the values of a song into a parsed layout template (see <see cref="LayoutTemplateSyntax"/>). Missing values
/// are filled in where a sensible value exists: a missing album artist becomes <see cref="VariousArtists"/> for a
/// compilation and the artist otherwise, a missing artist becomes the album artist. Otherwise a missing value renders
/// empty: an optional section with it vanishes, a folder name that ends up empty is left out, and separators left at
/// the ends of a name are removed (see <see cref="PathNameSanitizer"/>). No name such as "Unknown Artist" is invented,
/// since the scan would read it from the path as metadata. Pure.
/// </summary>
public static class LayoutTemplateRenderer
{
    /// <summary>
    /// The album artist of a compilation without album artist, so all its songs stay in one folder.
    /// </summary>
    public const string VariousArtists = "Various Artists";

    /// <summary>
    /// The file name when nothing is left of the rendered one.
    /// </summary>
    public const string EmptyBaseName = "_";

    /// <summary>
    /// Renders the path of a song.
    /// </summary>
    /// <param name="template">The parsed template.</param>
    /// <param name="values">The values of the song.</param>
    /// <param name="maxPathBytes">Longest relative path of a file in UTF-8 bytes (the limit of the scan).</param>
    /// <param name="longestExtension">The longest extension of the files of the song (without dot), or <c>null</c>;
    /// the file name leaves room for it.</param>
    /// <param name="fallbackBaseName">The file name if nothing is left of the rendered one, usually the current base
    /// name of the song.</param>
    public static RenderedPath Render(
        LayoutTemplate template,
        TemplateValues values,
        int maxPathBytes,
        string? longestExtension = null,
        string? fallbackBaseName = null)
    {
        int extensionBytes = string.IsNullOrEmpty(longestExtension)
            ? 0
            : PathNameSanitizer.ByteCountOf(longestExtension) + 1;
        List<string> folders = [];
        foreach (IReadOnlyList<LayoutTemplatePart> segment in template.Folders)
        {
            string folder = PathNameSanitizer.SanitizeName(RenderSegment(segment, values));
            if (folder.Length > 0)
            {
                folders.Add(folder);
            }
        }

        string baseName = PathNameSanitizer.SanitizeName(RenderSegment(template.FileName, values), extensionBytes);
        if (baseName.Length == 0)
        {
            baseName = PathNameSanitizer.SanitizeName(fallbackBaseName ?? string.Empty, extensionBytes);
        }

        if (baseName.Length == 0)
        {
            baseName = EmptyBaseName;
        }

        int pathBytes = folders.Sum(folder => PathNameSanitizer.ByteCountOf(folder) + 1)
            + PathNameSanitizer.ByteCountOf(baseName)
            + extensionBytes;
        return new RenderedPath(folders, baseName, pathBytes > maxPathBytes);
    }

    /// <summary>
    /// The text of a value with the fallbacks described above, without characters that names cannot hold; empty if
    /// the song has no such value.
    /// </summary>
    public static string ValueOf(TemplateField field, TemplateValues values, int? width = null)
    {
        string? text = field switch
        {
            TemplateField.Title => values.Title,
            TemplateField.Artist => Text(values.Artist) ?? Text(values.AlbumArtist),
            TemplateField.AlbumArtist => Text(values.AlbumArtist)
                ?? (values.IsCompilation ? VariousArtists : Text(values.Artist)),
            TemplateField.Album => values.Album,
            TemplateField.Genre => values.Genre,
            TemplateField.Year => Number(values.Year, width),
            TemplateField.TrackNumber => Number(values.TrackNumber, width),
            TemplateField.DiscNumber => Number(values.DiscNumber, width),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
        return Text(text) ?? string.Empty;
    }

    private static string RenderSegment(IReadOnlyList<LayoutTemplatePart> parts, TemplateValues values)
    {
        StringBuilder builder = new();
        foreach (LayoutTemplatePart part in parts)
        {
            switch (part)
            {
                case LayoutTemplateText text:
                    builder.Append(text.Text);
                    break;
                case LayoutTemplatePlaceholder placeholder:
                    builder.Append(ValueOf(placeholder.Field, values, placeholder.Width));
                    break;
                case LayoutTemplateSection section:
                    builder.Append(RenderSection(section, values));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parts), part, "Unknown part.");
            }
        }

        return builder.ToString();
    }

    private static string RenderSection(LayoutTemplateSection section, TemplateValues values)
    {
        StringBuilder builder = new();
        foreach (LayoutTemplatePart part in section.Parts)
        {
            if (part is LayoutTemplatePlaceholder placeholder)
            {
                string value = ValueOf(placeholder.Field, values, placeholder.Width);
                if (value.Length == 0)
                {
                    return string.Empty;
                }

                builder.Append(value);
            }
            else if (part is LayoutTemplateText text)
            {
                builder.Append(text.Text);
            }
        }

        return builder.ToString();
    }

    private static string? Text(string? value)
    {
        return value is null ? null : MetadataText.Normalize(PathNameSanitizer.ReplaceInvalidCharacters(value));
    }

    private static string? Number(int? value, int? width)
    {
        if (value is not > 0)
        {
            return null;
        }

        return width is { } digits
            ? value.Value.ToString("D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
