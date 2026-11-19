using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Templates;
using WaveMP.Core.Themes.Validations;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Storage;

[TestFixture]
public class InstalledThemeCheckTests
{
    private const string GlassPath = "images/glass.svg";

    /// <summary>
    /// A theme as the store installs it: its description and its files by their installed names.
    /// </summary>
    private static (InstalledTheme Theme, Dictionary<string, byte[]> Files) Install(byte[] package)
    {
        using MemoryStream stream = new(package);
        ValidatedTheme validated = ThemePackageValidator.Validate(stream, new ThemeOptions()).Theme!;
        InstalledTheme theme = new()
        {
            Id = validated.Manifest.Id,
            Name = validated.Manifest.Name,
            Author = validated.Manifest.Author,
            Version = validated.Manifest.Version,
            ThemeApi = validated.Manifest.ThemeApi,
            Variants = validated.Manifest.Variants,
            Stylesheet = validated.Stylesheet.Name,
            Previews = validated.Previews,
            Files = [.. validated.Files.Select(file => file.Name)],
            SizeBytes = validated.Files.Sum(file => (long)file.Content.Length),
            InstalledAt = DateTimeOffset.UnixEpoch,
            Sources = validated.Sources,
        };
        return (theme, validated.Files.ToDictionary(file => file.Name, file => file.Content, StringComparer.Ordinal));
    }

    private static (InstalledTheme Theme, Dictionary<string, byte[]> Files) Install(ThemePackageBuilder? builder = null)
    {
        return Install((builder ?? new ThemePackageBuilder()).Build());
    }

    private static string NameOf(InstalledTheme theme, string source)
    {
        return theme.Sources.Single(pair => pair.Value == source).Key;
    }

    [Test]
    public void Run_ThemeInstalledWithTheseRules_HasNoProblems()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install(new ThemePackageBuilder().WithFont()
            .With("theme.css", ThemePackageBuilder.Stylesheet + "\n.a { mask: url(\"images/glass.svg#icon\"); }")
            .With(
                GlassPath,
                """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
                  <metadata>drawing program</metadata>
                  <style>rect { fill: url(#shine); }</style>
                  <defs><linearGradient id="shine"><stop offset="0" stop-color="#fff"/></linearGradient></defs>
                  <text x="1" y="2">a &amp; b
                  c</text>
                  <rect width="10" height="10" style="fill: url('#shine')"/>
                </svg>
                """));

        InstalledThemeCheck.Run(theme, files).Should().BeEmpty();
    }

    [Test]
    public void Run_TheTemplate_HasNoProblems()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install(ThemeTemplate.Zip);

        InstalledThemeCheck.Run(theme, files).Should().BeEmpty();
    }

    [Test]
    public void Run_ThemeApiThatIsNoLongerSupported_ReportsIt()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();

        IReadOnlyList<ThemeError> problems =
            InstalledThemeCheck.Run(theme with { ThemeApi = ThemeApiVersions.Oldest - 1 }, files);

        problems.Should().ContainSingle().Which.Should().Be(new ThemeError(
            ThemeErrorCode.UnsupportedApiVersion,
            "manifest.json",
            Field: "themeApi",
            Min: ThemeApiVersions.Oldest,
            Max: ThemeApiVersions.Current));
    }

    [Test]
    public void Run_SvgThatTheseRulesReject_NamesItsPathInThePackage()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        files[NameOf(theme, GlassPath)] = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

        InstalledThemeCheck.Run(theme, files).Should().Equal(new ThemeError(ThemeErrorCode.SvgScript, GlassPath));
    }

    [Test]
    public void Run_SvgThatTheseRulesCleanDifferently_ReportsSvgNotCleaned()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        files[NameOf(theme, GlassPath)] = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><animate attributeName="x"/><rect width="1"/></svg>""");

        InstalledThemeCheck.Run(theme, files).Should().Equal(new ThemeError(ThemeErrorCode.SvgNotCleaned, GlassPath));
    }

    [Test]
    public void Run_StylesheetThatTheseRulesReject_ReportsItsLine()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        files[theme.Stylesheet] = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(files[theme.Stylesheet]) + "\n@import 'more.css';\n");

        InstalledThemeCheck.Run(theme, files).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Code = ThemeErrorCode.StylesheetImport,
            Path = "theme.css",
        });
    }

    [TestCase("/api/v1/themes/borealis/files/00000000000000000000000000000000.svg", ThemeErrorCode.StylesheetAbsoluteUrl)]
    [TestCase("/images/glass.svg", ThemeErrorCode.StylesheetAbsoluteUrl)]
    [TestCase("images/glass.svg", ThemeErrorCode.StylesheetMissingFile)]
    [TestCase("/api/v1/themes/aurora/files/00000000000000000000000000000000.svg", ThemeErrorCode.StylesheetMissingFile)]
    public void Run_StylesheetWithAnAddressOfNoInstalledFile_ReportsIt(string address, ThemeErrorCode code)
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        files[theme.Stylesheet] = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(files[theme.Stylesheet]) + $"\n.b {{ background: url(\"{address}\"); }}\n");

        InstalledThemeCheck.Run(theme, files).Should().ContainSingle().Which.Code.Should().Be(code);
    }

    [Test]
    public void Run_ThemeWithoutSources_NamesTheInstalledFiles()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        string glass = NameOf(theme, GlassPath);
        files[glass] = Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" onload="x()"/>""");

        IReadOnlyList<ThemeError> problems =
            InstalledThemeCheck.Run(theme with { Sources = new Dictionary<string, string>() }, files);

        problems.Should().Equal(new ThemeError(ThemeErrorCode.SvgEventHandler, glass));
    }

    [Test]
    public void Run_PictureWithAnotherFormat_ReportsIt()
    {
        (InstalledTheme theme, Dictionary<string, byte[]> files) = Install();
        string preview = theme.Previews[ThemeVariant.Light];
        files[preview] = [1, 2, 3];

        InstalledThemeCheck.Run(theme, files).Should().Equal(new ThemeError(
            ThemeErrorCode.ImageInvalid,
            "preview-dark.png"));
    }
}
