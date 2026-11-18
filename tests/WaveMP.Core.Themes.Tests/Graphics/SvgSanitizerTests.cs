using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Graphics;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Tests.Graphics;

[TestFixture]
public class SvgSanitizerTests
{
    private const string Path = "images/a.svg";

    private static (string? Svg, List<ThemeError> Errors) Sanitize(string svg)
    {
        List<ThemeError> errors = [];
        byte[]? result = SvgSanitizer.Sanitize(Path, Encoding.UTF8.GetBytes(svg), errors);
        return (result is null ? null : Encoding.UTF8.GetString(result), errors);
    }

    private static ThemeErrorCode Rejected(string svg)
    {
        (string? result, List<ThemeError> errors) = Sanitize(svg);
        result.Should().BeNull();
        return errors.Should().ContainSingle().Which.Code;
    }

    private static string Svg(string content)
    {
        return $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10">{content}</svg>""";
    }

    [Test]
    public void Sanitize_PlainDrawing_KeepsIt()
    {
        string svg = Svg(
            """<defs><linearGradient id="g"><stop offset="0" stop-color="#fff"/></linearGradient></defs><rect width="10" height="10" fill="url(#g)" style="opacity: 0.5"/><use href="#g"/>""");

        (string? result, List<ThemeError> errors) = Sanitize(svg);

        errors.Should().BeEmpty();
        result.Should().Be(
            """<svg viewBox="0 0 10 10" xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="g"><stop offset="0" stop-color="#fff" /></linearGradient></defs><rect width="10" height="10" fill="url(#g)" style="opacity: 0.5" /><use href="#g" /></svg>""");
    }

    [Test]
    public void Sanitize_XlinkHrefIntoTheFile_IsWrittenAsHref()
    {
        (string? result, _) = Sanitize(Svg("""<use xlink:href="#a"/>"""));

        result.Should().Contain("""<use href="#a" />""");
    }

    [Test]
    public void Sanitize_MetadataOfDrawingProgramsAndAnimations_IsRemoved()
    {
        string svg =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <?xml-stylesheet href="https://example.com/a.css"?>
            <!-- drawn with a program -->
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"
                 xmlns:sodipodi="http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd" inkscape:version="1.3"
                 width="10" height="10" data-x="1">
              <sodipodi:namedview pagecolor="#ffffff"/>
              <metadata><title>x</title></metadata>
              <rect width="10" height="10" inkscape:label="Layer"><animate attributeName="width" to="5"/></rect>
              <set attributeName="fill" to="red"/>
            </svg>
            """;

        (string? result, List<ThemeError> errors) = Sanitize(svg);

        errors.Should().BeEmpty();
        result.Should().NotContain("inkscape").And.NotContain("sodipodi").And.NotContain("metadata")
            .And.NotContain("animate").And.NotContain("<set").And.NotContain("data-x")
            .And.NotContain("example.com").And.NotContain("drawn with");
        result.Should().Contain("<rect width=\"10\" height=\"10\" />");
    }

    [Test]
    public void Sanitize_EmbeddedRasterPicture_IsKept()
    {
        (string? result, List<ThemeError> errors) = Sanitize(Svg("""<image href="data:image/png;base64,iVBORw0KGgo=" width="1" height="1"/>"""));

        errors.Should().BeEmpty();
        result.Should().Contain("data:image/png;base64,iVBORw0KGgo=");
    }

    [TestCase("<script>alert(1)</script>")]
    [TestCase("<script href=\"https://example.com/a.js\"/>")]
    [TestCase("<g><g><script><![CDATA[alert(1)]]></script></g></g>")]
    [TestCase("<metadata><script>alert(1)</script></metadata>")]
    [TestCase("<SCRIPT>alert(1)</SCRIPT>")]
    [TestCase("<html:script xmlns:html=\"http://www.w3.org/1999/xhtml\">alert(1)</html:script>")]
    public void Sanitize_Script_IsRejected(string content)
    {
        Rejected(Svg(content)).Should().Be(ThemeErrorCode.SvgScript);
    }

    [TestCase("<rect onload=\"alert(1)\"/>")]
    [TestCase("<rect ONCLICK=\"alert(1)\"/>")]
    [TestCase("<g onmouseover=\"alert(1)\"/>")]
    [TestCase("<sodipodi:x xmlns:sodipodi=\"urn:x\" onload=\"alert(1)\"/>")]
    [TestCase("<rect x:onload=\"alert(1)\" xmlns:x=\"urn:x\"/>")]
    public void Sanitize_EventHandler_IsRejected(string content)
    {
        Rejected(Svg(content)).Should().Be(ThemeErrorCode.SvgEventHandler);
    }

    [TestCase("<foreignObject><div xmlns=\"http://www.w3.org/1999/xhtml\">x</div></foreignObject>")]
    [TestCase("<iframe src=\"https://example.com\"/>")]
    [TestCase("<div xmlns=\"http://www.w3.org/1999/xhtml\">x</div>")]
    [TestCase("<embed src=\"https://example.com/a.swf\"/>")]
    public void Sanitize_ForeignContent_IsRejected(string content)
    {
        Rejected(Svg(content)).Should().Be(ThemeErrorCode.SvgForeignObject);
    }

    [TestCase("<use href=\"https://example.com/sprite.svg#a\"/>")]
    [TestCase("<use xlink:href=\"other.svg#a\"/>")]
    [TestCase("<image href=\"https://example.com/a.png\"/>")]
    [TestCase("<image href=\"data:image/svg+xml;base64,PHN2Zz4=\"/>")]
    [TestCase("<a href=\"javascript:alert(1)\"><rect/></a>")]
    [TestCase("<a xlink:href=\" JaVaScRiPt:alert(1)\"><rect/></a>")]
    [TestCase("<feImage href=\"https://example.com/a.png\"/>")]
    [TestCase("<rect fill=\"url(https://example.com/a.svg#g)\"/>")]
    [TestCase("<rect fill=\"u\\72l(https://example.com/a.svg#g)\"/>")]
    [TestCase("<rect style=\"background: url('https://example.com/a.png')\"/>")]
    [TestCase("<rect mask=\"src('https://example.com/a.png')\"/>")]
    [TestCase("<style>@import url(https://example.com/a.css);</style>")]
    [TestCase("<style>rect { fill: url(https://example.com/a.svg#g); }</style>")]
    [TestCase("<style><![CDATA[rect { background: url(//example.com/a.png); }]]></style>")]
    public void Sanitize_ExternalReference_IsRejected(string content)
    {
        Rejected(Svg(content)).Should().Be(ThemeErrorCode.SvgExternalReference);
    }

    [TestCase("<!DOCTYPE svg [<!ENTITY a \"b\">]><svg xmlns=\"http://www.w3.org/2000/svg\">&a;</svg>")]
    [TestCase("<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\"><svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [TestCase("<!doctype svg SYSTEM \"file:///etc/passwd\"><svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    public void Sanitize_DocumentTypeOrEntities_AreRejected(string svg)
    {
        Rejected(svg).Should().Be(ThemeErrorCode.SvgEntity);
    }

    [TestCase("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect>")]
    [TestCase("<svg><rect/></svg>")]
    [TestCase("<html xmlns=\"http://www.w3.org/1999/xhtml\"/>")]
    [TestCase("not xml at all")]
    [TestCase("<svg xmlns=\"http://www.w3.org/2000/svg\">&undefined;</svg>")]
    public void Sanitize_NoSvgDocument_IsRejected(string svg)
    {
        Rejected(svg).Should().Be(ThemeErrorCode.SvgInvalid);
    }
}
