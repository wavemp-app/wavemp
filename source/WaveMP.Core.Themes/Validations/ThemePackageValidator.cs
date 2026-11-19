using System.Security.Cryptography;
using System.Text;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Fonts;
using WaveMP.Core.Themes.Graphics;
using WaveMP.Core.Themes.Manifests;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Packages;
using WaveMP.Core.Themes.Stylesheets;

namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// Checks an uploaded theme package completely and turns it into the files to install: the structure of the ZIP file
/// (<see cref="ThemePackageReader"/>), the manifest (<see cref="ThemeManifestReader"/>), the SVG files (cleaned by
/// <see cref="SvgSanitizer"/>), the PNG and WebP files (by their signature), the WOFF2 fonts (by their header, see
/// <see cref="WebFonts"/>) and the CSS file (<see cref="ThemeStylesheet"/>, which rewrites its addresses to the
/// installed files). All errors are collected, so a theme author sees every problem at once.
/// </summary>
public static class ThemePackageValidator
{
    /// <summary>
    /// Most errors reported for a package.
    /// </summary>
    public const int MaxErrors = 50;

    private const int HashBytes = 16;

    // Used for the addresses while the manifest has no valid id; the package is rejected anyway.
    private const string PlaceholderId = "invalid";

    // Used for the address of a picture or font whose content was rejected; the package is rejected anyway.
    private const string RejectedFileName = "rejected";

    /// <summary>
    /// Checks a package. <paramref name="expectedId"/> is the id of the theme the package replaces, if any.
    /// </summary>
    public static ThemeValidationResult Validate(Stream content, ThemeOptions options, string? expectedId = null)
    {
        ThemePackage package = ThemePackageReader.Read(content, options);
        if (package.Errors.Count > 0)
        {
            return Rejected(package.Errors);
        }

        List<ThemeError> errors = [];
        ThemeManifest? manifest = null;
        if (package.Files.TryGetValue(PackagePaths.ManifestFileName, out byte[]? manifestContent))
        {
            manifest = ThemeManifestReader.Read(manifestContent, errors);
        }
        else
        {
            errors.Add(new ThemeError(ThemeErrorCode.ManifestMissing));
        }

        if (manifest is not null && expectedId is not null && manifest.Id != expectedId)
        {
            errors.Add(new ThemeError(
                ThemeErrorCode.ManifestIdMismatch,
                PackagePaths.ManifestFileName,
                Field: ThemeManifestReader.IdField));
        }

        string themeId = manifest?.Id ?? PlaceholderId;
        Dictionary<string, ValidatedThemeFile> files = CheckFiles(package, errors);
        CheckPreviews(manifest, package, errors);
        ValidatedThemeFile? stylesheet = CheckStylesheet(package, themeId, files, errors);

        if (errors.Count > 0 || manifest is null || stylesheet is null)
        {
            return Rejected(errors);
        }

        return new ThemeValidationResult
        {
            Theme = new ValidatedTheme
            {
                Manifest = manifest,
                Stylesheet = stylesheet,
                Files = [stylesheet, .. files.Values.DistinctBy(file => file.Name)],
                Previews = manifest.Previews.ToDictionary(pair => pair.Key, pair => files[pair.Value].Name),
            },
            Errors = [],
        };
    }

    /// <summary>
    /// The pictures and fonts of the package, checked, by their path in the package.
    /// </summary>
    private static Dictionary<string, ValidatedThemeFile> CheckFiles(ThemePackage package, List<ThemeError> errors)
    {
        Dictionary<string, ValidatedThemeFile> files = new(StringComparer.Ordinal);
        foreach ((string path, byte[] content) in package.Files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            byte[]? checkedContent = PackagePaths.KindOf(path) switch
            {
                PackageFileKind.Svg => SvgSanitizer.Sanitize(path, content, errors),
                PackageFileKind.Png => CheckSignature(path, content, RasterImages.IsPng(content), errors),
                PackageFileKind.WebP => CheckSignature(path, content, RasterImages.IsWebP(content), errors),
                PackageFileKind.Font =>
                    CheckSignature(path, content, WebFonts.IsWoff2(content), errors, ThemeErrorCode.FontInvalid),
                _ => null,
            };
            if (checkedContent is not null)
            {
                files[path] = ToFile(checkedContent, Path.GetExtension(path).ToLowerInvariant());
            }
        }

        return files;
    }

    /// <summary>
    /// A file that is installed as it is, if its content is of the format its extension names.
    /// </summary>
    private static byte[]? CheckSignature(
        string path,
        byte[] content,
        bool matches,
        List<ThemeError> errors,
        ThemeErrorCode code = ThemeErrorCode.ImageInvalid)
    {
        if (matches)
        {
            return content;
        }

        errors.Add(new ThemeError(code, path));
        return null;
    }

    /// <summary>
    /// Every preview is a picture of the package; a picture with a wrong content is reported by
    /// <see cref="CheckFiles"/>.
    /// </summary>
    private static void CheckPreviews(ThemeManifest? manifest, ThemePackage package, List<ThemeError> errors)
    {
        if (manifest is null)
        {
            return;
        }

        foreach ((ThemeVariant variant, string path) in manifest.Previews)
        {
            bool isPicture = PackagePaths.KindOf(path) is PackageFileKind.Svg or PackageFileKind.Png or
                PackageFileKind.WebP;
            if (!isPicture || !package.Files.ContainsKey(path))
            {
                errors.Add(new ThemeError(
                    ThemeErrorCode.PreviewMissing,
                    PackagePaths.ManifestFileName,
                    Field: $"{ThemeManifestReader.PreviewsField}.{ThemeManifestReader.VariantName(variant)}",
                    File: path));
            }
        }
    }

    private static ValidatedThemeFile? CheckStylesheet(
        ThemePackage package,
        string themeId,
        Dictionary<string, ValidatedThemeFile> files,
        List<ThemeError> errors)
    {
        List<string> stylesheets = [.. package.Files.Keys
            .Where(path => PackagePaths.KindOf(path) == PackageFileKind.Stylesheet)
            .Order(StringComparer.Ordinal)];
        if (stylesheets.Count == 0)
        {
            errors.Add(new ThemeError(ThemeErrorCode.StylesheetMissing));
            return null;
        }

        foreach (string extra in stylesheets.Skip(1))
        {
            errors.Add(new ThemeError(ThemeErrorCode.MultipleStylesheets, extra));
        }

        string path = stylesheets[0];
        string? css = ThemeStylesheet.Check(path, package.Files[path], AddressOf, errors);
        return css is null ? null : ToFile(Encoding.UTF8.GetBytes(css), ".css");

        // A picture or font whose content was rejected is in the package: its own error says what is wrong with it, so
        // the CSS file is not reported as naming a missing file. The package is rejected anyway.
        string? AddressOf(string target)
        {
            if (files.TryGetValue(target, out ValidatedThemeFile? file))
            {
                return ThemeFiles.AddressOf(themeId, file.Name);
            }

            return package.Files.ContainsKey(target) && IsInstalledKind(PackagePaths.KindOf(target))
                ? ThemeFiles.AddressOf(themeId, RejectedFileName)
                : null;
        }
    }

    private static bool IsInstalledKind(PackageFileKind kind)
    {
        return kind is PackageFileKind.Svg or PackageFileKind.Png or PackageFileKind.WebP or PackageFileKind.Font;
    }

    private static ValidatedThemeFile ToFile(byte[] content, string extension)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(content).AsSpan(0, HashBytes));
        return new ValidatedThemeFile(hash + extension, content);
    }

    private static ThemeValidationResult Rejected(IEnumerable<ThemeError> errors)
    {
        return new ThemeValidationResult { Theme = null, Errors = [.. errors.Take(MaxErrors)] };
    }
}
