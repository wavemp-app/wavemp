using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Templates;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Tests.Templates;

[TestFixture]
public class ThemeTemplateTests
{
    [Test]
    public void Paths_AreTheFilesOfTheTemplateFolder()
    {
        ThemeTemplate.Paths.Should().Contain([
            "README.md",
            "manifest.json",
            "theme.css",
            "images/cover-placeholder.svg",
            "preview-light.svg",
            "preview-dark.svg",
        ]);
    }

    [Test]
    public void Zip_HoldsEveryFileOfTheTemplate()
    {
        using ZipArchive archive = new(new MemoryStream(ThemeTemplate.Zip), ZipArchiveMode.Read);

        archive.Entries.Select(entry => entry.FullName).Should().Equal(ThemeTemplate.Paths);
    }

    [Test]
    public void Zip_IsTheSameEveryTime()
    {
        ThemeTemplate.Zip.Should().BeSameAs(ThemeTemplate.Zip);
    }

    [Test]
    public void Zip_PassesTheChecksOfAnUpload()
    {
        using MemoryStream stream = new(ThemeTemplate.Zip);

        ThemeValidationResult result = ThemePackageValidator.Validate(stream, new ThemeOptions());

        result.Errors.Should().BeEmpty();
        result.Theme!.Manifest.ThemeApi.Should().Be(ThemeApiVersions.Current);
        result.Theme.Manifest.Variants.Should().Equal(ThemeVariant.Light, ThemeVariant.Dark);
        string css = Encoding.UTF8.GetString(result.Theme.Stylesheet.Content);
        css.Should().Contain("url(\"/api/v1/themes/my-theme/files/");
    }
}
