using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Services;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Services;

[TestFixture]
public class ThemeServiceTests
{
    private TemporaryDirectory _folder = null!;
    private ThemeStore _store = null!;
    private IUserRepository _users = null!;
    private ThemeService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _folder = new TemporaryDirectory();
        IOptions<ThemeOptions> options = Microsoft.Extensions.Options.Options.Create(
            new ThemeOptions { RootPath = _folder.Combine("themes") });
        _store = new ThemeStore(options, TimeProvider.System, NullLogger<ThemeStore>.Instance);
        await _store.InitializeAsync();
        _users = Substitute.For<IUserRepository>();
        _service = new ThemeService(_store, _users, options, NullLogger<ThemeService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    [Test]
    public async Task InstallAsync_ValidPackage_InstallsTheTheme()
    {
        using MemoryStream package = new ThemePackageBuilder().BuildStream();

        ThemeInstallResult result = await _service.InstallAsync(package);

        result.Status.Should().Be(ThemeInstallStatus.Installed);
        result.Theme!.Id.Should().Be("aurora");
        _service.List().Should().ContainSingle().Which.Id.Should().Be("aurora");
    }

    [Test]
    public async Task InstallAsync_SameIdTwice_ReturnsAlreadyInstalled()
    {
        using MemoryStream first = new ThemePackageBuilder().BuildStream();
        using MemoryStream second = new ThemePackageBuilder().BuildStream();
        await _service.InstallAsync(first);

        (await _service.InstallAsync(second)).Status.Should().Be(ThemeInstallStatus.AlreadyInstalled);
    }

    [Test]
    public async Task InstallAsync_NoZip_ReturnsUnsupportedFormat()
    {
        using MemoryStream content = new(Encoding.UTF8.GetBytes("<svg/>"));

        (await _service.InstallAsync(content)).Status.Should().Be(ThemeInstallStatus.UnsupportedFormat);
    }

    [Test]
    public async Task InstallAsync_InvalidPackage_ReturnsTheErrorsAndInstallsNothing()
    {
        using MemoryStream package = new ThemePackageBuilder().With("theme.css", "@import 'x.css';").BuildStream();

        ThemeInstallResult result = await _service.InstallAsync(package);

        result.Status.Should().Be(ThemeInstallStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.StylesheetImport);
        _service.List().Should().BeEmpty();
    }

    [Test]
    public async Task ReplaceAsync_InstalledTheme_ReplacesIt()
    {
        using MemoryStream first = new ThemePackageBuilder().BuildStream();
        using MemoryStream second = new ThemePackageBuilder()
            .WithManifest(ThemePackageBuilder.Manifest(name: "Aurora 2"))
            .BuildStream();
        await _service.InstallAsync(first);

        ThemeInstallResult result = await _service.ReplaceAsync("aurora", second);

        result.Status.Should().Be(ThemeInstallStatus.Replaced);
        _service.Find("aurora")!.Name.Should().Be("Aurora 2");
    }

    [Test]
    public async Task ReplaceAsync_PackageOfAnotherTheme_IsRejected()
    {
        using MemoryStream first = new ThemePackageBuilder().BuildStream();
        using MemoryStream other = new ThemePackageBuilder("borealis").BuildStream();
        await _service.InstallAsync(first);

        ThemeInstallResult result = await _service.ReplaceAsync("aurora", other);

        result.Status.Should().Be(ThemeInstallStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.ManifestIdMismatch);
    }

    [Test]
    public async Task ReplaceAsync_MissingTheme_ReturnsNotFound()
    {
        using MemoryStream package = new ThemePackageBuilder().BuildStream();

        (await _service.ReplaceAsync("aurora", package)).Status.Should().Be(ThemeInstallStatus.NotFound);
    }

    [Test]
    public async Task DeleteAsync_InstalledTheme_GivesItsProfilesTheDefaultTheme()
    {
        using MemoryStream package = new ThemePackageBuilder().BuildStream();
        await _service.InstallAsync(package);

        (await _service.DeleteAsync("aurora")).Should().BeTrue();

        await _users.Received(1).ResetThemeAsync("aurora", Arg.Any<CancellationToken>());
        _service.List().Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_MissingTheme_ChangesNoProfile()
    {
        (await _service.DeleteAsync("aurora")).Should().BeFalse();

        await _users.DidNotReceiveWithAnyArgs().ResetThemeAsync(default!, default);
    }
}
