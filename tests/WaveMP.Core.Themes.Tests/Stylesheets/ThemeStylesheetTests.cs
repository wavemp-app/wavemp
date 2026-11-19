using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Stylesheets;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Tests.Stylesheets;

/// <summary>
/// The CSS file of an uploaded theme: only relative paths to pictures of the package, in any spelling, and nothing that
/// loads from elsewhere, in any spelling either.
/// </summary>
[TestFixture]
public class ThemeStylesheetTests
{
    private const string Path = "theme.css";

    private static readonly Dictionary<string, string> _pictures = new()
    {
        ["images/glass.svg"] = "/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg",
        ["images/deep/orb.png"] = "/api/v1/themes/aurora/files/fedcba9876543210fedcba9876543210.png",
        ["preview.webp"] = "/api/v1/themes/aurora/files/00000000000000000000000000000000.webp",
    };

    private static (string? Css, List<ThemeError> Errors) Check(string css, string path = Path)
    {
        List<ThemeError> errors = [];
        string? result = ThemeStylesheet.Check(
            path,
            Encoding.UTF8.GetBytes(css),
            target => _pictures.GetValueOrDefault(target),
            errors);
        return (result, errors);
    }

    [Test]
    public void Check_InstalledStylesheet_KeepsTheAddressesOfItsFilesAsTheyAre()
    {
        const string Css = """
            a { background: url("/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg#glow"); }
            b { mask: image-set("/api/v1/themes/aurora/files/fedcba9876543210fedcba9876543210.png" 1x); }
            """;
        List<ThemeError> errors = [];

        string? result = ThemeStylesheet.Check(
            Path,
            Encoding.UTF8.GetBytes(Css),
            target => _pictures.GetValueOrDefault(target),
            errors,
            address => _pictures.FirstOrDefault(pair => pair.Value == address).Key);

        errors.Should().BeEmpty();
        result.Should().Be(Css);
    }

    [Test]
    public void Check_InstalledStylesheet_StillRejectsOtherAbsoluteAddresses()
    {
        List<ThemeError> errors = [];

        string? result = ThemeStylesheet.Check(
            Path,
            Encoding.UTF8.GetBytes("a { b: url(/api/v1/themes/borealis/files/x.svg); }"),
            target => _pictures.GetValueOrDefault(target),
            errors,
            _ => null);

        result.Should().BeNull();
        errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.StylesheetAbsoluteUrl);
    }

    private static ThemeErrorCode SingleCode(string css)
    {
        (string? result, List<ThemeError> errors) = Check(css);
        result.Should().BeNull();
        return errors.Should().ContainSingle().Subject.Code;
    }

    [TestCase("a { background: url(images/glass.svg); }")]
    [TestCase("a { background: url('images/glass.svg'); }")]
    [TestCase("a { background: url(\"images/glass.svg\"); }")]
    [TestCase("a { background: URL( images/glass.svg ); }")]
    [TestCase("a { background: url(./images/glass.svg); }")]
    [TestCase("a { background: url(images/../images/glass.svg); }")]
    [TestCase("a { background: u\\72l(images/glass.svg); }")]
    [TestCase("a { background: url(images/gl\\61ss.svg); }")]
    [TestCase("a { background: src('images/glass.svg'); }")]
    public void Check_RelativePathToAPicture_IsRewrittenToTheInstalledFile(string css)
    {
        (string? result, List<ThemeError> errors) = Check(css);

        errors.Should().BeEmpty();
        result.Should().Be(
            "a { background: url(\"/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg\"); }");
    }

    [Test]
    public void Check_PathInAVariable_IsRewrittenToo()
    {
        (string? result, _) = Check(":root { --wmp-image-play-button: url('images/glass.svg') center / 100% no-repeat; }");

        result.Should().Be(
            ":root { --wmp-image-play-button: url(\"/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg\") center / 100% no-repeat; }");
    }

    [Test]
    public void Check_CssFileInAFolder_ResolvesPathsAgainstItsFolder()
    {
        (string? result, List<ThemeError> errors) = Check("a { b: url(../preview.webp); }", "styles/theme.css");

        errors.Should().BeEmpty();
        result.Should().Contain("00000000000000000000000000000000.webp");
    }

    [Test]
    public void Check_FragmentOfAPicture_IsKept()
    {
        (string? result, _) = Check("a { b: url(images/glass.svg#shine); }");

        result.Should().Be("a { b: url(\"/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg#shine\"); }");
    }

    [Test]
    public void Check_FragmentOnly_IsKeptAsReferenceIntoTheDocument()
    {
        (string? result, List<ThemeError> errors) = Check("a { filter: url(#glow); }");

        errors.Should().BeEmpty();
        result.Should().Be("a { filter: url(\"#glow\"); }");
    }

    [Test]
    public void Check_ImageSetStrings_AreAddresses()
    {
        (string? result, List<ThemeError> errors) = Check("a { b: image-set('images/glass.svg' 1x, url(images/deep/orb.png) 2x); }");

        errors.Should().BeEmpty();
        result.Should().Be(
            "a { b: image-set(\"/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.svg\" 1x, " +
            "url(\"/api/v1/themes/aurora/files/fedcba9876543210fedcba9876543210.png\") 2x); }");
    }

    [Test]
    public void Check_StringsElsewhere_StayUnchanged()
    {
        const string Css = "a::before { content: 'http://example.com'; font-family: 'Segoe UI'; }";

        (string? result, List<ThemeError> errors) = Check(Css);

        errors.Should().BeEmpty();
        result.Should().Be(Css);
    }

    [Test]
    public void Check_CommentsWithAddresses_AreKeptAsTheyAre()
    {
        const string Css = "/* url(https://example.com/a.png) @import 'x'; */ a { color: red; }";

        (string? result, _) = Check(Css);

        result.Should().Be(Css);
    }

    [Test]
    public void Check_ByteOrderMark_IsRemoved()
    {
        List<ThemeError> errors = [];
        byte[] content = [0xEF, 0xBB, 0xBF, .. "a{}"u8];

        ThemeStylesheet.Check(Path, content, _ => null, errors).Should().Be("a{}");
    }

    [TestCase("a { b: url(https://example.com/a.png); }")]
    [TestCase("a { b: url(http://example.com/a.png); }")]
    [TestCase("a { b: url('HTTPS://example.com/a.png'); }")]
    [TestCase("a { b: url(//example.com/a.png); }")]
    [TestCase("a { b: url(\\/\\/example.com/a.png); }")]
    [TestCase("a { b: url(data:image/svg+xml,%3Csvg%3E); }")]
    [TestCase("a { b: url('data:image/png;base64,AAAA'); }")]
    [TestCase("a { b: url('javascript:alert(1)'); }")]
    [TestCase("a { b: url(blob:https://example.com/x); }")]
    [TestCase("a { b: url(http\\3a //example.com/a.png); }")]
    [TestCase("a { b: url(\\68 ttp://example.com/a.png); }")]
    [TestCase("a { b: u\\72l(https://example.com/a.png); }")]
    [TestCase("a { b: \\75 rl('https://example.com/a.png'); }")]
    [TestCase("a { b: src('https://example.com/a.png'); }")]
    [TestCase("a { b: image-set('https://example.com/a.png' 1x); }")]
    [TestCase("a { b: -webkit-image-set('//example.com/a.png' 1x); }")]
    [TestCase("a { b: image('https://example.com/a.png'); }")]
    [TestCase("a { b: url(images\\5c ..\\5c ..\\5c example.com); }")]
    [TestCase("@font-face { font-family: x; src: url(https://example.com/f.woff2); }")]
    [TestCase("a { cursor: url(https://example.com/c.cur), auto; }")]
    public void Check_ExternalAddress_IsRejected(string css)
    {
        SingleCode(css).Should().Be(ThemeErrorCode.StylesheetExternalUrl);
    }

    [TestCase("a { b: url(/themes/midnight/images/glass-button.svg); }")]
    [TestCase("a { b: url('/api/v1/me'); }")]
    public void Check_RootAbsolutePath_IsRejected(string css)
    {
        SingleCode(css).Should().Be(ThemeErrorCode.StylesheetAbsoluteUrl);
    }

    [TestCase("a { b: url(); }")]
    [TestCase("a { b: url(''); }")]
    [TestCase("a { b: url(images/glass.svg?v=1); }")]
    [TestCase("a { b: url(../../etc/passwd); }")]
    [TestCase("a { b: url(images/%2e%2e/glass.svg); }")]
    [TestCase("a { b: url('images/gl ass.svg'); }")]
    [TestCase("a { b: url('h\\9 ttp://example.com/a.png'); }")]
    [TestCase("a { b: url('ht\\a tp://example.com/a.png'); }")]
    [TestCase("a { b: src(var(--address)); }")]
    [TestCase("a { b: url('images/glass.svg' crossorigin(anonymous)); }")]
    [TestCase("a { b: image-set(var(--address) 1x); }")]
    [TestCase("a { b: image(attr(data-image)); }")]
    public void Check_InvalidAddress_IsRejected(string css)
    {
        SingleCode(css).Should().Be(ThemeErrorCode.StylesheetInvalidUrl);
    }

    [Test]
    public void Check_AddressOfAMissingFile_NamesTheFile()
    {
        (_, List<ThemeError> errors) = Check("a {\n  b: url(images/missing.svg);\n}");

        errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(ThemeErrorCode.StylesheetMissingFile, Path, 2, File: "images/missing.svg"));
    }

    [Test]
    public void Check_AddressOfTheManifest_IsRejectedAsMissingPicture()
    {
        SingleCode("a { b: url(manifest.json); }").Should().Be(ThemeErrorCode.StylesheetMissingFile);
    }

    [TestCase("@import url(other.css);")]
    [TestCase("@import 'https://example.com/a.css';")]
    [TestCase("@IMPORT 'a.css';")]
    [TestCase("@\\69mport 'a.css';")]
    [TestCase("@media screen { @import 'a.css'; }")]
    public void Check_Import_IsRejected(string css)
    {
        (_, List<ThemeError> errors) = Check(css);

        errors.Should().Contain(error => error.Code == ThemeErrorCode.StylesheetImport);
    }

    [Test]
    public void Check_Layer_IsRejected()
    {
        SingleCode("@layer theme { a { color: red; } }").Should().Be(ThemeErrorCode.StylesheetLayer);
    }

    [TestCase("@namespace svg url(http://www.w3.org/2000/svg);")]
    [TestCase("@-moz-document url-prefix() { a { color: red; } }")]
    [TestCase("@document domain(example.com) { }")]
    public void Check_OtherAtRules_AreRejected(string css)
    {
        (_, List<ThemeError> errors) = Check(css);

        errors.Should().Contain(error => error.Code == ThemeErrorCode.StylesheetAtRuleNotAllowed);
    }

    [Test]
    public void Check_AllowedAtRules_PassUnchanged()
    {
        const string Css =
            """
            @charset "utf-8";
            @media (width >= 64rem) { a { color: red; } }
            @supports (backdrop-filter: blur(1px)) { a { color: red; } }
            @container stage (width < 32rem) { a { color: red; } }
            @keyframes glow { from { opacity: 0; } to { opacity: 1; } }
            @property --wmp-x { syntax: '<color>'; inherits: true; initial-value: red; }
            @font-face { font-family: Own; src: local('Arial'); }
            @starting-style { a { opacity: 0; } }
            """;

        (string? result, List<ThemeError> errors) = Check(Css);

        errors.Should().BeEmpty();
        result.Should().Be(Css.ReplaceLineEndings("\n"));
    }

    [TestCase("a { b: url(a b); }")]
    [TestCase("a { content: 'broken\n}")]
    public void Check_BrokenTokens_AreRejected(string css)
    {
        (_, List<ThemeError> errors) = Check(css);

        errors.Should().Contain(error => error.Code == ThemeErrorCode.StylesheetSyntaxError);
    }

    [Test]
    public void Check_NoUtf8_IsRejected()
    {
        List<ThemeError> errors = [];
        byte[] utf16 = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("a{}")];

        ThemeStylesheet.Check(Path, utf16, _ => null, errors).Should().BeNull();

        errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.StylesheetInvalidEncoding);
    }

    [Test]
    public void Check_ManyProblems_ReportsEachWithItsLineUpToTheLimit()
    {
        string css = string.Join('\n', Enumerable.Range(0, 30).Select(_ => "a { b: url(https://x/y); }"));

        (_, List<ThemeError> errors) = Check(css);

        errors.Should().HaveCount(ThemeStylesheet.MaxErrors);
        errors.Select(error => error.Line).Should().BeInAscendingOrder().And.StartWith(1);
    }
}
