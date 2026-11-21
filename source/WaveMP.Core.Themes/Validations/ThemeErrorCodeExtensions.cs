namespace WaveMP.Core.Themes.Validations;

public static class ThemeErrorCodeExtensions
{
    /// <summary>
    /// The codes of the security rules: constructs in CSS or SVG files that could load or run something outside the
    /// theme, and content the checks cannot read reliably and therefore cannot vouch for. An installed theme that
    /// fails one of them with the rules of this version is blocked (see <see cref="Storage.InstalledThemeStatus"/>).
    /// </summary>
    private static readonly HashSet<ThemeErrorCode> _securityRules =
    [
        ThemeErrorCode.StylesheetInvalidEncoding,
        ThemeErrorCode.StylesheetSyntaxError,
        ThemeErrorCode.StylesheetImport,
        ThemeErrorCode.StylesheetAtRuleNotAllowed,
        ThemeErrorCode.StylesheetExternalUrl,
        ThemeErrorCode.StylesheetAbsoluteUrl,
        ThemeErrorCode.StylesheetInvalidUrl,
        ThemeErrorCode.SvgInvalid,
        ThemeErrorCode.SvgEntity,
        ThemeErrorCode.SvgScript,
        ThemeErrorCode.SvgEventHandler,
        ThemeErrorCode.SvgForeignObject,
        ThemeErrorCode.SvgExternalReference,
        ThemeErrorCode.SvgNotCleaned,
    ];

    /// <summary>
    /// Whether the code names a broken security rule. Other codes, e.g. an older theme API version, a missing file
    /// or <c>@layer</c>, only make an installed theme outdated.
    /// </summary>
    public static bool IsSecurityRule(this ThemeErrorCode code)
    {
        return _securityRules.Contains(code);
    }
}
