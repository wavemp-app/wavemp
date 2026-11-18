using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Validations;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Validations;

[TestFixture]
public class ThemePackageValidatorTests
{
    private static ThemeValidationResult Validate(ThemePackageBuilder builder, string? expectedId = null)
    {
        using MemoryStream stream = builder.BuildStream();
        return ThemePackageValidator.Validate(stream, new ThemeOptions(), expectedId);
    }

    [Test]
    public void Validate_ValidPackage_ReturnsTheFilesUnderTheirContentNames()
    {
        ThemeValidationResult result = Validate(new ThemePackageBuilder());

        result.Errors.Should().BeEmpty();
        ValidatedTheme theme = result.Theme!;
        theme.Manifest.Id.Should().Be("aurora");
        theme.Files.Should().OnlyContain(file => ThemeFiles.IsValidName(file.Name));
        theme.Files[0].Should().Be(theme.Stylesheet);
        theme.Stylesheet.Name.Should().EndWith(".css");
        // Both previews have the same content and are installed once.
        theme.Files.Should().HaveCount(3);
        theme.Previews[ThemeVariant.Light].Should().Be(theme.Previews[ThemeVariant.Dark]).And.EndWith(".png");
    }

    [Test]
    public void Validate_ValidPackage_PointsTheCssToTheInstalledPictures()
    {
        ValidatedTheme theme = Validate(new ThemePackageBuilder()).Theme!;

        string css = Encoding.UTF8.GetString(theme.Stylesheet.Content);
        string svg = theme.Files.Single(file => file.Name.EndsWith(".svg", StringComparison.Ordinal)).Name;
        css.Should().Contain($"url(\"/api/v1/themes/aurora/files/{svg}\") center / 100% 100% no-repeat")
            .And.Contain($"background: url(\"/api/v1/themes/aurora/files/{svg}\");")
            .And.NotContain("images/glass.svg");
    }

    [Test]
    public void Validate_SamePackageTwice_GivesTheSameNames()
    {
        ValidatedTheme first = Validate(new ThemePackageBuilder()).Theme!;
        ValidatedTheme second = Validate(new ThemePackageBuilder()).Theme!;

        second.Files.Select(file => file.Name).Should().Equal(first.Files.Select(file => file.Name));
    }

    [Test]
    public void Validate_ChangedPicture_ChangesTheNameOfTheCssToo()
    {
        ValidatedTheme first = Validate(new ThemePackageBuilder()).Theme!;
        ValidatedTheme changed = Validate(new ThemePackageBuilder().With(
            "images/glass.svg",
            ThemePackageBuilder.GlassSvg.Replace("#7fb2e5", "#000000", StringComparison.Ordinal))).Theme!;

        changed.Stylesheet.Name.Should().NotBe(first.Stylesheet.Name);
    }

    [Test]
    public void Validate_SvgFiles_AreInstalledCleaned()
    {
        ValidatedTheme theme = Validate(new ThemePackageBuilder().With(
            "images/glass.svg",
            """<svg xmlns="http://www.w3.org/2000/svg"><!-- note --><metadata>m</metadata><rect width="1"/></svg>""")).Theme!;

        string svg = Encoding.UTF8.GetString(theme.Files.Single(file => file.Name.EndsWith(".svg", StringComparison.Ordinal)).Content);
        svg.Should().Be("""<svg xmlns="http://www.w3.org/2000/svg"><rect width="1" /></svg>""");
    }

    [Test]
    public void Validate_NoManifest_IsRejected()
    {
        ThemeValidationResult result = Validate(new ThemePackageBuilder().Without("manifest.json"));

        result.Theme.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.ManifestMissing);
    }

    [Test]
    public void Validate_NoStylesheet_IsRejected()
    {
        Validate(new ThemePackageBuilder().Without("theme.css")).Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ThemeErrorCode.StylesheetMissing);
    }

    [Test]
    public void Validate_TwoStylesheets_AreRejected()
    {
        Validate(new ThemePackageBuilder().With("extra.css", "a{}")).Errors.Should().ContainSingle()
            .Which.Should().Be(new ThemeError(ThemeErrorCode.MultipleStylesheets, "theme.css"));
    }

    [Test]
    public void Validate_PreviewNotInThePackage_IsRejected()
    {
        Validate(new ThemePackageBuilder().Without("preview-dark.png")).Errors.Should().ContainSingle()
            .Which.Should().Be(new ThemeError(
                ThemeErrorCode.PreviewMissing,
                "manifest.json",
                Field: "previews.dark",
                File: "preview-dark.png"));
    }

    [Test]
    public void Validate_PictureWithTheWrongContent_IsRejected()
    {
        Validate(new ThemePackageBuilder().With("preview-light.png", "<html><script>alert(1)</script></html>"))
            .Errors.Should().ContainSingle().Which.Should().Be(
                new ThemeError(ThemeErrorCode.ImageInvalid, "preview-light.png"));
    }

    [Test]
    public void Validate_WebPWithPngContent_IsRejected()
    {
        Validate(new ThemePackageBuilder().With("images/a.webp", ThemePackageBuilder.Png))
            .Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.ImageInvalid);
    }

    [Test]
    public void Validate_OtherIdThanTheReplacedTheme_IsRejected()
    {
        Validate(new ThemePackageBuilder("aurora"), expectedId: "borealis").Errors.Should().ContainSingle()
            .Which.Should().Be(new ThemeError(ThemeErrorCode.ManifestIdMismatch, "manifest.json", Field: "id"));
    }

    [Test]
    public void Validate_MaliciousPackage_ReportsEveryProblemAtOnce()
    {
        ThemePackageBuilder builder = new ThemePackageBuilder("midnight")
            .With("theme.css", "@import 'https://example.com/a.css';\na { b: url(https://example.com/x.png); }")
            .With("images/evil.svg", """<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"/>""");

        ThemeValidationResult result = Validate(builder);

        result.Theme.Should().BeNull();
        result.Errors.Select(error => error.Code).Should().BeEquivalentTo([
            ThemeErrorCode.ManifestReservedId,
            ThemeErrorCode.SvgEventHandler,
            ThemeErrorCode.StylesheetImport,
            ThemeErrorCode.StylesheetExternalUrl,
        ]);
    }

    [Test]
    public void Validate_BrokenStructure_StopsBeforeTheContent()
    {
        ThemeValidationResult result = Validate(new ThemePackageBuilder()
            .With("../evil.css", "a{}")
            .With("theme.css", "@import 'x';"));

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.InvalidPath);
    }
}
