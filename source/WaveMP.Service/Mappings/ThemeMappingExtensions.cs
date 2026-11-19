using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ThemeMappingExtensions
{
    public static ThemeDto ToDto(this InstalledTheme theme)
    {
        return new ThemeDto
        {
            Id = theme.Id,
            Name = theme.Name,
            Author = theme.Author,
            Version = theme.Version,
            ThemeApi = theme.ThemeApi,
            Variants = [.. theme.Variants.Select(variant => variant.ToDto())],
            StylesheetUrl = ThemeFiles.AddressOf(theme.Id, theme.Stylesheet),
            Previews = [.. theme.Variants
                .Where(theme.Previews.ContainsKey)
                .Select(variant => new ThemePreviewDto
                {
                    Variant = variant.ToDto(),
                    Url = ThemeFiles.AddressOf(theme.Id, theme.Previews[variant]),
                })],
            SizeBytes = theme.SizeBytes,
            InstalledAt = theme.InstalledAt,
            Status = theme.IsOutdated ? ThemeStatusDto.Outdated : ThemeStatusDto.Current,
            Problems = [.. theme.Problems.Select(problem => problem.ToDto())],
        };
    }

    public static ThemeVariantDto ToDto(this ThemeVariant variant)
    {
        return variant switch
        {
            ThemeVariant.Light => ThemeVariantDto.Light,
            ThemeVariant.Dark => ThemeVariantDto.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown theme variant."),
        };
    }

    public static ThemeErrorDto ToDto(this ThemeError error)
    {
        return new ThemeErrorDto
        {
            Code = error.Code.ToDto(),
            Path = error.Path,
            Line = error.Line,
            Field = error.Field,
            File = error.File,
            Min = error.Min,
            Max = error.Max,
        };
    }

    public static ThemeErrorCodeDto ToDto(this ThemeErrorCode code)
    {
        return code switch
        {
            ThemeErrorCode.PackageCorrupt => ThemeErrorCodeDto.PackageCorrupt,
            ThemeErrorCode.PackageTooManyEntries => ThemeErrorCodeDto.PackageTooManyEntries,
            ThemeErrorCode.PackageTooLarge => ThemeErrorCodeDto.PackageTooLarge,
            ThemeErrorCode.FileTooLarge => ThemeErrorCodeDto.FileTooLarge,
            ThemeErrorCode.InvalidPath => ThemeErrorCodeDto.InvalidPath,
            ThemeErrorCode.DuplicatePath => ThemeErrorCodeDto.DuplicatePath,
            ThemeErrorCode.UnsupportedFile => ThemeErrorCodeDto.UnsupportedFile,
            ThemeErrorCode.ManifestMissing => ThemeErrorCodeDto.ManifestMissing,
            ThemeErrorCode.ManifestInvalidJson => ThemeErrorCodeDto.ManifestInvalidJson,
            ThemeErrorCode.ManifestMissingField => ThemeErrorCodeDto.ManifestMissingField,
            ThemeErrorCode.ManifestInvalidField => ThemeErrorCodeDto.ManifestInvalidField,
            ThemeErrorCode.ManifestReservedId => ThemeErrorCodeDto.ManifestReservedId,
            ThemeErrorCode.ManifestIdMismatch => ThemeErrorCodeDto.ManifestIdMismatch,
            ThemeErrorCode.UnsupportedApiVersion => ThemeErrorCodeDto.UnsupportedApiVersion,
            ThemeErrorCode.PreviewMissing => ThemeErrorCodeDto.PreviewMissing,
            ThemeErrorCode.StylesheetMissing => ThemeErrorCodeDto.StylesheetMissing,
            ThemeErrorCode.MultipleStylesheets => ThemeErrorCodeDto.MultipleStylesheets,
            ThemeErrorCode.StylesheetInvalidEncoding => ThemeErrorCodeDto.StylesheetInvalidEncoding,
            ThemeErrorCode.StylesheetSyntaxError => ThemeErrorCodeDto.StylesheetSyntaxError,
            ThemeErrorCode.StylesheetImport => ThemeErrorCodeDto.StylesheetImport,
            ThemeErrorCode.StylesheetLayer => ThemeErrorCodeDto.StylesheetLayer,
            ThemeErrorCode.StylesheetAtRuleNotAllowed => ThemeErrorCodeDto.StylesheetAtRuleNotAllowed,
            ThemeErrorCode.StylesheetExternalUrl => ThemeErrorCodeDto.StylesheetExternalUrl,
            ThemeErrorCode.StylesheetAbsoluteUrl => ThemeErrorCodeDto.StylesheetAbsoluteUrl,
            ThemeErrorCode.StylesheetInvalidUrl => ThemeErrorCodeDto.StylesheetInvalidUrl,
            ThemeErrorCode.StylesheetMissingFile => ThemeErrorCodeDto.StylesheetMissingFile,
            ThemeErrorCode.SvgInvalid => ThemeErrorCodeDto.SvgInvalid,
            ThemeErrorCode.SvgEntity => ThemeErrorCodeDto.SvgEntity,
            ThemeErrorCode.SvgScript => ThemeErrorCodeDto.SvgScript,
            ThemeErrorCode.SvgEventHandler => ThemeErrorCodeDto.SvgEventHandler,
            ThemeErrorCode.SvgForeignObject => ThemeErrorCodeDto.SvgForeignObject,
            ThemeErrorCode.SvgExternalReference => ThemeErrorCodeDto.SvgExternalReference,
            ThemeErrorCode.ImageInvalid => ThemeErrorCodeDto.ImageInvalid,
            ThemeErrorCode.FontInvalid => ThemeErrorCodeDto.FontInvalid,
            ThemeErrorCode.SvgNotCleaned => ThemeErrorCodeDto.SvgNotCleaned,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown theme error code."),
        };
    }
}
