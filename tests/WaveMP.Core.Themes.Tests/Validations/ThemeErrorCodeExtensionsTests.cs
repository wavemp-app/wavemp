using AwesomeAssertions;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Tests.Validations;

[TestFixture]
public class ThemeErrorCodeExtensionsTests
{
    [TestCase(ThemeErrorCode.SvgScript)]
    [TestCase(ThemeErrorCode.SvgEventHandler)]
    [TestCase(ThemeErrorCode.SvgForeignObject)]
    [TestCase(ThemeErrorCode.SvgExternalReference)]
    [TestCase(ThemeErrorCode.SvgEntity)]
    [TestCase(ThemeErrorCode.SvgInvalid)]
    [TestCase(ThemeErrorCode.SvgNotCleaned)]
    [TestCase(ThemeErrorCode.StylesheetExternalUrl)]
    [TestCase(ThemeErrorCode.StylesheetAbsoluteUrl)]
    [TestCase(ThemeErrorCode.StylesheetInvalidUrl)]
    [TestCase(ThemeErrorCode.StylesheetImport)]
    [TestCase(ThemeErrorCode.StylesheetAtRuleNotAllowed)]
    [TestCase(ThemeErrorCode.StylesheetSyntaxError)]
    [TestCase(ThemeErrorCode.StylesheetInvalidEncoding)]
    public void IsSecurityRule_UnsafeConstructInCssOrSvg_IsTrue(ThemeErrorCode code)
    {
        code.IsSecurityRule().Should().BeTrue();
    }

    [TestCase(ThemeErrorCode.UnsupportedApiVersion)]
    [TestCase(ThemeErrorCode.ManifestInvalidField)]
    [TestCase(ThemeErrorCode.PreviewMissing)]
    [TestCase(ThemeErrorCode.StylesheetLayer)]
    [TestCase(ThemeErrorCode.StylesheetMissingFile)]
    [TestCase(ThemeErrorCode.FileTooLarge)]
    [TestCase(ThemeErrorCode.ImageInvalid)]
    [TestCase(ThemeErrorCode.FontInvalid)]
    public void IsSecurityRule_OtherRule_IsFalse(ThemeErrorCode code)
    {
        code.IsSecurityRule().Should().BeFalse();
    }
}
