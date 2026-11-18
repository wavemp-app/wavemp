using System.Text.RegularExpressions;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;

namespace WaveMP.Core.Themes.Tests.Definitions;

/// <summary>
/// The server knows the shipped themes and the version of the theme API only as constants; these tests keep them in
/// line with the web app and THEMING.md.
/// </summary>
[TestFixture]
public partial class ThemeDefinitionsTests
{
    [Test]
    public void BuiltInThemes_Ids_AreTheThemesShippedWithTheWebApp()
    {
        string themes = Path.Combine(FindRepositoryRoot(), "assets", "web", "themes");

        IEnumerable<string?> shipped = Directory.GetDirectories(themes).Select(Path.GetFileName);

        BuiltInThemes.Ids.Should().BeEquivalentTo(shipped);
    }

    [Test]
    public void ThemeApiVersions_Current_IsTheVersionOfThemingMdAndTheWebApp()
    {
        string root = FindRepositoryRoot();
        string theming = File.ReadAllText(Path.Combine(root, "THEMING.md"));
        string parts = File.ReadAllText(Path.Combine(root, "source", "WaveMP.Web", "src", "themes", "parts.ts"));

        int documented = int.Parse(ThemingVersion().Match(theming).Groups[1].Value);
        int webApp = int.Parse(WebAppVersion().Match(parts).Groups[1].Value);

        ThemeApiVersions.Current.Should().Be(documented).And.Be(webApp);
        ThemeApiVersions.Oldest.Should().BeLessThanOrEqualTo(ThemeApiVersions.Current);
    }

    [TestCase("aurora", true)]
    [TestCase("deep-sea-2", true)]
    [TestCase("a", true)]
    [TestCase("Aurora", false)]
    [TestCase("-aurora", false)]
    [TestCase("aurora--sky", false)]
    [TestCase("aurora\n", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void ThemeIdRules_IsValid_AcceptsSlugs(string? id, bool expected)
    {
        ThemeIdRules.IsValid(id).Should().Be(expected);
    }

    [Test]
    public void ThemeIdRules_MaxLength_FitsTheSettingsOfAProfile()
    {
        ThemeIdRules.MaxLength.Should().BeLessThanOrEqualTo(Data.Entities.UserSettings.ThemeIdMaxLength);
    }

    [TestCase("0123456789abcdef0123456789abcdef.css", true)]
    [TestCase("0123456789abcdef0123456789abcdef.webp", true)]
    [TestCase("0123456789ABCDEF0123456789abcdef.css", false)]
    [TestCase("0123456789abcdef0123456789abcdef.js", false)]
    [TestCase("../0123456789abcdef0123456789abcde.css", false)]
    [TestCase("theme.json", false)]
    public void ThemeFiles_IsValidName_AcceptsContentNamesOnly(string name, bool expected)
    {
        ThemeFiles.IsValidName(name).Should().Be(expected);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaveMP.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root (WaveMP.slnx) was not found.");
    }

    [GeneratedRegex(@"^Version (\d+)$", RegexOptions.Multiline)]
    private static partial Regex ThemingVersion();

    [GeneratedRegex(@"export const themeApiVersion = (\d+)")]
    private static partial Regex WebAppVersion();
}
