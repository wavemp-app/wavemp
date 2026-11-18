using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Why a theme package is rejected. Clients translate it; the members of <see cref="ThemeErrorDto"/> named in
/// brackets carry the details.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ThemeErrorCodeDto>))]
public enum ThemeErrorCodeDto
{
    /// <summary>
    /// The ZIP file is damaged, encrypted or uses a compression the server cannot read.
    /// </summary>
    [JsonStringEnumMemberName("package.corrupt")]
    PackageCorrupt,

    /// <summary>
    /// The package holds more entries than allowed (max: the number).
    /// </summary>
    [JsonStringEnumMemberName("package.too_many_entries")]
    PackageTooManyEntries,

    /// <summary>
    /// The files of the package are larger than allowed once unpacked (max: bytes).
    /// </summary>
    [JsonStringEnumMemberName("package.too_large")]
    PackageTooLarge,

    /// <summary>
    /// A file is larger than allowed once unpacked (path; max: bytes).
    /// </summary>
    [JsonStringEnumMemberName("file.too_large")]
    FileTooLarge,

    /// <summary>
    /// A path is absolute, leaves the package, is too deep or too long, or has characters other than letters,
    /// digits, ".", "_" and "-" (path).
    /// </summary>
    [JsonStringEnumMemberName("file.invalid_path")]
    InvalidPath,

    /// <summary>
    /// Two entries have the same path, also when only the case differs (path).
    /// </summary>
    [JsonStringEnumMemberName("file.duplicate_path")]
    DuplicatePath,

    /// <summary>
    /// A file of a type a theme cannot contain, e.g. a script, a font or a nested archive (path).
    /// </summary>
    [JsonStringEnumMemberName("file.unsupported_type")]
    UnsupportedFile,

    /// <summary>
    /// The package has no manifest.json.
    /// </summary>
    [JsonStringEnumMemberName("manifest.missing")]
    ManifestMissing,

    /// <summary>
    /// manifest.json is no valid JSON object or names a member twice (path).
    /// </summary>
    [JsonStringEnumMemberName("manifest.invalid_json")]
    ManifestInvalidJson,

    /// <summary>
    /// A required member of the manifest is missing (field).
    /// </summary>
    [JsonStringEnumMemberName("manifest.missing_field")]
    ManifestMissingField,

    /// <summary>
    /// A member of the manifest has a wrong type or value (field).
    /// </summary>
    [JsonStringEnumMemberName("manifest.invalid_field")]
    ManifestInvalidField,

    /// <summary>
    /// The id of the manifest is the id of a theme shipped with WaveMP (field).
    /// </summary>
    [JsonStringEnumMemberName("manifest.reserved_id")]
    ManifestReservedId,

    /// <summary>
    /// The id of the manifest is not the id of the theme that is replaced (field).
    /// </summary>
    [JsonStringEnumMemberName("manifest.id_mismatch")]
    ManifestIdMismatch,

    /// <summary>
    /// The theme is written for a version of the theme API this server does not support (field; min and max: the
    /// supported versions).
    /// </summary>
    [JsonStringEnumMemberName("manifest.unsupported_api_version")]
    UnsupportedApiVersion,

    /// <summary>
    /// A preview picture named in the manifest is not in the package or no picture (field; file).
    /// </summary>
    [JsonStringEnumMemberName("manifest.preview_missing")]
    PreviewMissing,

    /// <summary>
    /// The package has no CSS file.
    /// </summary>
    [JsonStringEnumMemberName("css.missing")]
    StylesheetMissing,

    /// <summary>
    /// The package has more than one CSS file (path: a further one).
    /// </summary>
    [JsonStringEnumMemberName("css.multiple")]
    MultipleStylesheets,

    /// <summary>
    /// The CSS file is no valid UTF-8 text (path).
    /// </summary>
    [JsonStringEnumMemberName("css.invalid_encoding")]
    StylesheetInvalidEncoding,

    /// <summary>
    /// The CSS file has a broken string or address (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.syntax_error")]
    StylesheetSyntaxError,

    /// <summary>
    /// The CSS file uses @import (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.import")]
    StylesheetImport,

    /// <summary>
    /// The CSS file uses @layer; a theme stays outside the layers of the base (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.layer")]
    StylesheetLayer,

    /// <summary>
    /// The CSS file uses an at-rule themes cannot use, e.g. @namespace (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.at_rule_not_allowed")]
    StylesheetAtRuleNotAllowed,

    /// <summary>
    /// The CSS file names an external address: a scheme such as https: or data:, or // (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.external_url")]
    StylesheetExternalUrl,

    /// <summary>
    /// The CSS file names a root-absolute address such as /images/a.svg instead of a path relative to the CSS file
    /// (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.absolute_url")]
    StylesheetAbsoluteUrl,

    /// <summary>
    /// The CSS file names an address that is empty, has a query, leaves the package or has characters a path cannot
    /// have, or builds it from var(), attr() or env() (path, line).
    /// </summary>
    [JsonStringEnumMemberName("css.invalid_url")]
    StylesheetInvalidUrl,

    /// <summary>
    /// The CSS file names a file that is not in the package or is no picture (path, line; file: the missing file).
    /// </summary>
    [JsonStringEnumMemberName("css.missing_file")]
    StylesheetMissingFile,

    /// <summary>
    /// An SVG file is no well-formed SVG document (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.invalid")]
    SvgInvalid,

    /// <summary>
    /// An SVG file has a document type declaration or entities (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.entity")]
    SvgEntity,

    /// <summary>
    /// An SVG file has a script (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.script")]
    SvgScript,

    /// <summary>
    /// An SVG file has an event handler attribute such as onload (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.event_handler")]
    SvgEventHandler,

    /// <summary>
    /// An SVG file embeds foreign content: foreignObject or HTML (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.foreign_object")]
    SvgForeignObject,

    /// <summary>
    /// An SVG file references something outside itself, e.g. another file, a website or a javascript: link (path).
    /// </summary>
    [JsonStringEnumMemberName("svg.external_reference")]
    SvgExternalReference,

    /// <summary>
    /// A PNG or WebP file is no picture of the format its extension names (path).
    /// </summary>
    [JsonStringEnumMemberName("image.invalid")]
    ImageInvalid,
}
