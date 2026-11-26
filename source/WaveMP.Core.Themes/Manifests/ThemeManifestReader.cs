using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Packages;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Manifests;

/// <summary>
/// Reads and checks <c>manifest.json</c> of a theme package:
/// <code>
/// {
///   "id": "aurora",
///   "name": "Aurora",
///   "author": "Jane Doe",
///   "version": "1.0.0",
///   "themeApi": 9,
///   "variants": ["light", "dark"],
///   "previews": { "light": "preview-light.webp", "dark": "preview-dark.webp" }
/// }
/// </code>
/// Comments and trailing commas are allowed, duplicate members are not; other members are ignored. The preview
/// pictures are paths in the package; that they exist is checked with the files (see
/// <see cref="ThemePackageValidator"/>).
/// </summary>
internal static partial class ThemeManifestReader
{
    public const int MaxNameLength = 60;
    public const int MaxAuthorLength = 60;
    public const int MaxVersionLength = 32;

    public const string IdField = "id";
    public const string NameField = "name";
    public const string AuthorField = "author";
    public const string VersionField = "version";
    public const string ThemeApiField = "themeApi";
    public const string VariantsField = "variants";
    public const string PreviewsField = "previews";

    private static readonly JsonDocumentOptions _options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 8,
        AllowDuplicateProperties = false,
    };

    /// <summary>
    /// The manifest, or <c>null</c> with the reasons in <paramref name="errors"/>.
    /// </summary>
    public static ThemeManifest? Read(byte[] content, List<ThemeError> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, _options);
        }
        catch (JsonException exception)
        {
            int? line = exception.LineNumber is { } number ? (int)number + 1 : null;
            errors.Add(new ThemeError(ThemeErrorCode.ManifestInvalidJson, PackagePaths.ManifestFileName, line));
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ThemeError(ThemeErrorCode.ManifestInvalidJson, PackagePaths.ManifestFileName));
                return null;
            }

            return Read(document.RootElement, errors);
        }
    }

    private static ThemeManifest? Read(JsonElement root, List<ThemeError> errors)
    {
        int before = errors.Count;
        string? id = ReadText(root, IdField, ThemeIdRules.MaxLength, errors);
        if (id is not null && !ThemeIdRules.IsValid(id))
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, IdField));
            id = null;
        }
        else if (id is not null && BuiltInThemes.Ids.Contains(id))
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestReservedId, IdField));
            id = null;
        }

        string? name = ReadText(root, NameField, MaxNameLength, errors);
        string? author = ReadText(root, AuthorField, MaxAuthorLength, errors);
        string? version = ReadText(root, VersionField, MaxVersionLength, errors);
        if (version is not null && !VersionPattern().IsMatch(version))
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, VersionField));
        }

        int? themeApi = ReadThemeApi(root, errors);
        IReadOnlyList<ThemeVariant>? variants = ReadVariants(root, errors);
        IReadOnlyDictionary<ThemeVariant, string>? previews = variants is null ? null : ReadPreviews(
            root,
            variants,
            errors);

        if (errors.Count > before || id is null || name is null || author is null || version is null ||
            themeApi is null || variants is null || previews is null)
        {
            return null;
        }

        return new ThemeManifest
        {
            Id = id,
            Name = name,
            Author = author,
            Version = version,
            ThemeApi = themeApi.Value,
            Variants = variants,
            Previews = previews,
        };
    }

    private static string? ReadText(JsonElement root, string field, int maxLength, List<ThemeError> errors)
    {
        if (!root.TryGetProperty(field, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, field));
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, field));
            return null;
        }

        string value = element.GetString()!.Trim();
        if (value.Length == 0)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, field));
            return null;
        }

        if (value.Length > maxLength || !IsPlainText(value))
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, field) with { Max = maxLength });
            return null;
        }

        return value;
    }

    /// <summary>
    /// Whether a text has no control characters, no broken UTF-16 and no characters that change the direction of
    /// the text around it (which could make a name look like another one).
    /// </summary>
    private static bool IsPlainText(string value)
    {
        foreach (Rune rune in value.EnumerateRunes())
        {
            bool isDirectionControl = rune.Value is (>= 0x202A and <= 0x202E) or (>= 0x2066 and <= 0x2069);
            if (rune == Rune.ReplacementChar || Rune.IsControl(rune) || isDirectionControl)
            {
                return false;
            }
        }

        return true;
    }

    private static int? ReadThemeApi(JsonElement root, List<ThemeError> errors)
    {
        if (!root.TryGetProperty(ThemeApiField, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, ThemeApiField));
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int version))
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, ThemeApiField));
            return null;
        }

        if (!ThemeApiVersions.IsSupported(version))
        {
            errors.Add(FieldError(ThemeErrorCode.UnsupportedApiVersion, ThemeApiField) with
            {
                Min = ThemeApiVersions.Oldest,
                Max = ThemeApiVersions.Current,
            });
            return null;
        }

        return version;
    }

    private static List<ThemeVariant>? ReadVariants(JsonElement root, List<ThemeError> errors)
    {
        if (!root.TryGetProperty(VariantsField, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, VariantsField));
            return null;
        }

        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() is 0 or > 2)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, VariantsField));
            return null;
        }

        List<ThemeVariant> variants = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            ThemeVariant? variant = item.ValueKind == JsonValueKind.String ? ParseVariant(item.GetString()) : null;
            if (variant is null || variants.Contains(variant.Value))
            {
                errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, VariantsField));
                return null;
            }

            variants.Add(variant.Value);
        }

        return variants;
    }

    private static Dictionary<ThemeVariant, string>? ReadPreviews(
        JsonElement root,
        IReadOnlyList<ThemeVariant> variants,
        List<ThemeError> errors)
    {
        if (!root.TryGetProperty(PreviewsField, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, PreviewsField));
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, PreviewsField));
            return null;
        }

        Dictionary<ThemeVariant, string> previews = [];
        foreach (ThemeVariant variant in variants)
        {
            string field = $"{PreviewsField}.{VariantName(variant)}";
            if (!element.TryGetProperty(VariantName(variant), out JsonElement path) ||
                path.ValueKind == JsonValueKind.Null)
            {
                errors.Add(FieldError(ThemeErrorCode.ManifestMissingField, field));
                continue;
            }

            if (path.ValueKind != JsonValueKind.String || !PackagePaths.IsValid(path.GetString()!))
            {
                errors.Add(FieldError(ThemeErrorCode.ManifestInvalidField, field));
                continue;
            }

            previews[variant] = path.GetString()!;
        }

        return previews.Count == variants.Count ? previews : null;
    }

    /// <summary>
    /// The name of a variant in the manifest and in the CSS (<c>data-theme-variant</c>).
    /// </summary>
    public static string VariantName(ThemeVariant variant)
    {
        return variant == ThemeVariant.Dark ? "dark" : "light";
    }

    private static ThemeVariant? ParseVariant(string? value)
    {
        return value switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => null,
        };
    }

    private static ThemeError FieldError(ThemeErrorCode code, string field)
    {
        return new ThemeError(code, PackagePaths.ManifestFileName, Field: field);
    }

    [GeneratedRegex(@"\A[0-9A-Za-z][0-9A-Za-z.+-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
