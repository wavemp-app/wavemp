namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// Why a theme package is rejected. The API hands these codes to the clients, which translate them.
/// </summary>
public enum ThemeErrorCode
{
    /// <summary>
    /// The ZIP file is damaged, encrypted or uses a compression the server cannot read.
    /// </summary>
    PackageCorrupt,

    /// <summary>
    /// The package holds more entries than allowed (limit: the number).
    /// </summary>
    PackageTooManyEntries,

    /// <summary>
    /// The files of the package are larger than allowed once unpacked (limit: bytes).
    /// </summary>
    PackageTooLarge,

    /// <summary>
    /// A file is larger than allowed once unpacked (path; limit: bytes).
    /// </summary>
    FileTooLarge,

    /// <summary>
    /// A path in the package is absolute, leaves the package, is too deep or too long, or has characters other than
    /// letters, digits, <c>.</c>, <c>_</c> and <c>-</c> (path).
    /// </summary>
    InvalidPath,

    /// <summary>
    /// Two entries have the same path, also when only the case differs (path).
    /// </summary>
    DuplicatePath,

    /// <summary>
    /// A file of a type that a theme cannot contain, e.g. a script, a font in another format than WOFF2 or a nested
    /// archive (path).
    /// </summary>
    UnsupportedFile,

    /// <summary>
    /// The package has no <c>manifest.json</c>.
    /// </summary>
    ManifestMissing,

    /// <summary>
    /// <c>manifest.json</c> is no valid JSON object (line, if known).
    /// </summary>
    ManifestInvalidJson,

    /// <summary>
    /// A required member of the manifest is missing (field).
    /// </summary>
    ManifestMissingField,

    /// <summary>
    /// A member of the manifest has a wrong type or value (field).
    /// </summary>
    ManifestInvalidField,

    /// <summary>
    /// The id of the manifest is the id of a theme shipped with WaveMP (field).
    /// </summary>
    ManifestReservedId,

    /// <summary>
    /// The id of the manifest is not the id of the theme that is replaced (field).
    /// </summary>
    ManifestIdMismatch,

    /// <summary>
    /// The theme is written for a version of the theme API this server does not support (field; min and max: the
    /// supported versions).
    /// </summary>
    UnsupportedApiVersion,

    /// <summary>
    /// A preview picture named in the manifest is not in the package or no picture (field; path).
    /// </summary>
    PreviewMissing,

    /// <summary>
    /// The package has no CSS file.
    /// </summary>
    StylesheetMissing,

    /// <summary>
    /// The package has more than one CSS file (path: the second one).
    /// </summary>
    MultipleStylesheets,

    /// <summary>
    /// The CSS file is no valid UTF-8 text (path).
    /// </summary>
    StylesheetInvalidEncoding,

    /// <summary>
    /// The CSS file has a broken string or address (path, line).
    /// </summary>
    StylesheetSyntaxError,

    /// <summary>
    /// The CSS file uses <c>@import</c> (path, line).
    /// </summary>
    StylesheetImport,

    /// <summary>
    /// The CSS file uses <c>@layer</c>; a theme stays outside the layers of the base (path, line).
    /// </summary>
    StylesheetLayer,

    /// <summary>
    /// The CSS file uses an at-rule that themes cannot use, e.g. <c>@namespace</c> (path, line).
    /// </summary>
    StylesheetAtRuleNotAllowed,

    /// <summary>
    /// The CSS file names an external address: a scheme such as <c>https:</c> or <c>data:</c>, or <c>//</c> (path,
    /// line).
    /// </summary>
    StylesheetExternalUrl,

    /// <summary>
    /// The CSS file names a root-absolute address such as <c>/images/a.svg</c>; theme files use paths relative to
    /// the CSS file (path, line).
    /// </summary>
    StylesheetAbsoluteUrl,

    /// <summary>
    /// The CSS file names an address that is empty, has a query, leaves the package or has characters a path in the
    /// package cannot have (path, line).
    /// </summary>
    StylesheetInvalidUrl,

    /// <summary>
    /// The CSS file names a file that is not in the package or is neither a picture nor a font (path, line; file: the
    /// missing file).
    /// </summary>
    StylesheetMissingFile,

    /// <summary>
    /// An SVG file is no well-formed SVG document (path).
    /// </summary>
    SvgInvalid,

    /// <summary>
    /// An SVG file has a document type declaration or entities (path).
    /// </summary>
    SvgEntity,

    /// <summary>
    /// An SVG file has a script (path).
    /// </summary>
    SvgScript,

    /// <summary>
    /// An SVG file has an event handler attribute such as <c>onload</c> (path).
    /// </summary>
    SvgEventHandler,

    /// <summary>
    /// An SVG file embeds foreign content (<c>foreignObject</c> or HTML) (path).
    /// </summary>
    SvgForeignObject,

    /// <summary>
    /// An SVG file references something outside itself, e.g. another file, a website or a <c>javascript:</c> link
    /// (path).
    /// </summary>
    SvgExternalReference,

    /// <summary>
    /// A PNG or WebP file is no picture of the format its extension names (path).
    /// </summary>
    ImageInvalid,

    /// <summary>
    /// A WOFF2 file is no WOFF2 font (path).
    /// </summary>
    FontInvalid,

    /// <summary>
    /// An SVG file of an installed theme still has content that this version of WaveMP removes from SVG files, e.g. an
    /// element it no longer allows. Only reported when an installed theme is checked again; uploading the theme again
    /// cleans the file (path).
    /// </summary>
    SvgNotCleaned,
}
