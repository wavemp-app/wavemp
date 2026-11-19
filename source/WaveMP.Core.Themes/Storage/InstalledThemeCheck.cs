using System.Text.Json;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Manifests;
using WaveMP.Core.Themes.Packages;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// Checks an installed theme again with the rules of this version of WaveMP. A theme is checked once when it is
/// uploaded; a later version may reject more in CSS or SVG files or no longer support the theme API version it is
/// written for. The installed files are checked as a package of their own (see
/// <see cref="ThemePackageValidator.ValidateInstalled"/>), with a <c>manifest.json</c> built from the description of
/// the theme. The problems name the files by their paths in the uploaded package where they are known.
/// </summary>
internal static class InstalledThemeCheck
{
    /// <summary>
    /// The problems of <paramref name="theme"/>, whose files are given by their installed names; empty if it passes.
    /// </summary>
    public static IReadOnlyList<ThemeError> Run(InstalledTheme theme, IReadOnlyDictionary<string, byte[]> files)
    {
        Dictionary<string, byte[]> content = new(files, StringComparer.Ordinal)
        {
            [PackagePaths.ManifestFileName] = Manifest(theme),
        };
        ThemePackage package = new() { Files = content, Errors = [] };
        ThemeValidationResult result = ThemePackageValidator.ValidateInstalled(package, theme.Id);
        return [.. result.Errors.Select(error => error with
        {
            Path = SourceOf(theme, error.Path),
            File = SourceOf(theme, error.File),
        })];
    }

    private static string? SourceOf(InstalledTheme theme, string? name)
    {
        return name is not null && theme.Sources.TryGetValue(name, out string? source) ? source : name;
    }

    /// <summary>
    /// The manifest the theme was installed with, as far as its description keeps it; the previews name the installed
    /// files.
    /// </summary>
    private static byte[] Manifest(InstalledTheme theme)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output))
        {
            writer.WriteStartObject();
            writer.WriteString(ThemeManifestReader.IdField, theme.Id);
            writer.WriteString(ThemeManifestReader.NameField, theme.Name);
            writer.WriteString(ThemeManifestReader.AuthorField, theme.Author);
            writer.WriteString(ThemeManifestReader.VersionField, theme.Version);
            writer.WriteNumber(ThemeManifestReader.ThemeApiField, theme.ThemeApi);
            writer.WriteStartArray(ThemeManifestReader.VariantsField);
            foreach (ThemeVariant variant in theme.Variants)
            {
                writer.WriteStringValue(ThemeManifestReader.VariantName(variant));
            }

            writer.WriteEndArray();
            writer.WriteStartObject(ThemeManifestReader.PreviewsField);
            foreach ((ThemeVariant variant, string preview) in theme.Previews)
            {
                writer.WriteString(ThemeManifestReader.VariantName(variant), preview);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return output.ToArray();
    }
}
