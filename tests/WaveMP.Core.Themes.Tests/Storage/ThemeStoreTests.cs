using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Testing.Files;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Storage;

[TestFixture]
public class ThemeStoreTests
{
    private TemporaryDirectory _folder = null!;
    private ThemeStore _store = null!;

    private ThemeOptions Options => new() { RootPath = _folder.Combine("themes") };

    [SetUp]
    public async Task SetUp()
    {
        _folder = new TemporaryDirectory();
        _store = CreateStore();
        await _store.InitializeAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    private ThemeStore CreateStore()
    {
        return new ThemeStore(
            Microsoft.Extensions.Options.Options.Create(Options),
            TimeProvider.System,
            NullLogger<ThemeStore>.Instance);
    }

    private static ValidatedTheme Theme(string id = "aurora", string name = "Aurora", string color = "#7fb2e5")
    {
        ThemePackageBuilder builder = new ThemePackageBuilder(id)
            .WithManifest(ThemePackageBuilder.Manifest(id, name: name))
            .With("images/glass.svg", ThemePackageBuilder.GlassSvg.Replace("#7fb2e5", color, StringComparison.Ordinal));
        using MemoryStream stream = builder.BuildStream();
        return ThemePackageValidator.Validate(stream, new ThemeOptions()).Theme!;
    }

    [Test]
    public async Task SaveAsync_NewTheme_WritesItsFilesAndDescription()
    {
        ValidatedTheme theme = Theme();

        ThemeStoreResult result = await _store.SaveAsync(theme, replace: false);

        result.Status.Should().Be(ThemeStoreStatus.Saved);
        InstalledTheme installed = result.Theme!;
        installed.Id.Should().Be("aurora");
        installed.Stylesheet.Should().Be(theme.Stylesheet.Name);
        installed.Variants.Should().Equal(ThemeVariant.Light, ThemeVariant.Dark);
        installed.SizeBytes.Should().Be(theme.Files.Sum(file => (long)file.Content.Length));
        File.Exists(_folder.Combine("themes/aurora/theme.json")).Should().BeTrue();
        foreach (ValidatedThemeFile file in theme.Files)
        {
            File.ReadAllBytes(_folder.Combine($"themes/aurora/files/{file.Name}")).Should().Equal(file.Content);
        }

        _store.List().Should().ContainSingle().Which.Should().Be(installed);
    }

    [Test]
    public async Task SaveAsync_ExistingIdWithoutReplace_ReturnsAlreadyInstalled()
    {
        await _store.SaveAsync(Theme(), replace: false);

        ThemeStoreResult result = await _store.SaveAsync(Theme(color: "#000000"), replace: false);

        result.Status.Should().Be(ThemeStoreStatus.AlreadyInstalled);
    }

    [Test]
    public async Task SaveAsync_ReplaceOfAMissingTheme_ReturnsNotFound()
    {
        (await _store.SaveAsync(Theme(), replace: true)).Status.Should().Be(ThemeStoreStatus.NotFound);
    }

    [Test]
    public async Task SaveAsync_Replace_SwapsTheFilesAndKeepsNoOldOnes()
    {
        ValidatedTheme first = Theme();
        await _store.SaveAsync(first, replace: false);
        ValidatedTheme second = Theme(color: "#000000");

        ThemeStoreResult result = await _store.SaveAsync(second, replace: true);

        result.Status.Should().Be(ThemeStoreStatus.Saved);
        Directory.GetFiles(_folder.Combine("themes/aurora/files")).Select(Path.GetFileName)
            .Should().BeEquivalentTo(second.Files.Select(file => file.Name));
        Directory.GetDirectories(_folder.Combine("themes")).Select(Path.GetFileName).Should().Equal("aurora");
        _store.Find("aurora")!.Stylesheet.Should().Be(second.Stylesheet.Name);
    }

    [Test]
    public async Task InitializeAsync_AfterARestart_ReadsTheInstalledThemes()
    {
        await _store.SaveAsync(Theme("borealis", "Borealis"), replace: false);
        await _store.SaveAsync(Theme("aurora", "Aurora"), replace: false);

        using ThemeStore restarted = CreateStore();
        await restarted.InitializeAsync();

        restarted.List().Select(theme => theme.Id).Should().Equal("aurora", "borealis");
        restarted.Find("aurora").Should().BeEquivalentTo(_store.Find("aurora"));
    }

    [Test]
    public async Task InitializeAsync_RemovesUnfinishedInstallationsAndSkipsBrokenThemes()
    {
        await _store.SaveAsync(Theme(), replace: false);
        Directory.CreateDirectory(_folder.Combine("themes/.staging-123/files"));
        Directory.CreateDirectory(_folder.Combine("themes/.trash-456"));
        Directory.CreateDirectory(_folder.Combine("themes/broken"));
        await File.WriteAllTextAsync(_folder.Combine("themes/broken/theme.json"), "{ not json");
        Directory.CreateDirectory(_folder.Combine("themes/midnight"));
        File.Copy(_folder.Combine("themes/aurora/theme.json"), _folder.Combine("themes/midnight/theme.json"));
        File.Delete(Directory.GetFiles(_folder.Combine("themes/aurora/files"))[0]);

        using ThemeStore restarted = CreateStore();
        await restarted.InitializeAsync();

        restarted.List().Should().BeEmpty();
        Directory.Exists(_folder.Combine("themes/.staging-123")).Should().BeFalse();
        Directory.Exists(_folder.Combine("themes/.trash-456")).Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_InstalledTheme_RemovesItsFolder()
    {
        await _store.SaveAsync(Theme(), replace: false);

        (await _store.DeleteAsync("aurora")).Should().BeTrue();
        (await _store.DeleteAsync("aurora")).Should().BeFalse();

        _store.List().Should().BeEmpty();
        Directory.GetDirectories(_folder.Combine("themes")).Should().BeEmpty();
    }

    [Test]
    public async Task OpenFile_FileOfTheTheme_ReturnsItsContent()
    {
        ValidatedTheme theme = Theme();
        await _store.SaveAsync(theme, replace: false);

        await using Stream? stream = _store.OpenFile("aurora", theme.Stylesheet.Name);

        using MemoryStream content = new();
        await stream!.CopyToAsync(content);
        content.ToArray().Should().Equal(theme.Stylesheet.Content);
    }

    [TestCase("aurora", "../theme.json")]
    [TestCase("aurora", "theme.json")]
    [TestCase("aurora", "00000000000000000000000000000000.css")]
    [TestCase("borealis", null)]
    [TestCase("..", null)]
    public async Task OpenFile_OtherFile_ReturnsNull(string id, string? name)
    {
        ValidatedTheme theme = Theme();
        await _store.SaveAsync(theme, replace: false);

        _store.OpenFile(id, name ?? theme.Stylesheet.Name).Should().BeNull();
    }
}
