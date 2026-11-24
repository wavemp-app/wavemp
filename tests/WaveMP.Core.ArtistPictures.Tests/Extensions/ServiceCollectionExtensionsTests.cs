using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using WaveMP.Core.ArtistPictures.Extensions;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Caching.Extensions;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.ArtistPictures.Tests.Extensions;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddWaveMpArtistPictures_WithItsDependencies_ResolvesAllServices()
    {
        using ServiceProvider provider = CreateServices([]).BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IArtistPictureService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IArtistPictureStore>().Should().NotBeNull();
    }

    [Test]
    public void AddWaveMpArtistPictures_WithoutSettings_UsesTheDefaults()
    {
        ArtistPictureOptions options = Resolve([]);

        options.RootPath.Should().Be(Path.Combine(Path.GetFullPath("/data"), "artist-pictures"));
        options.MaxUploadBytes.Should().Be(10 * 1024 * 1024);
        options.MaxDownloadBytes.Should().Be(20 * 1024 * 1024);
        options.MaxJsonBytes.Should().Be(2 * 1024 * 1024);
        options.LookupCacheDuration.Should().Be(TimeSpan.FromDays(7));
        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Test]
    public void AddWaveMpArtistPictures_WithSettings_PutsThePicturesIntoTheDataFolder()
    {
        using TemporaryDirectory data = new();

        ArtistPictureOptions options = Resolve(new()
        {
            ["DATA_PATH"] = data.Path,
            ["ArtistPictures:RootPath"] = "/ignored",
            ["ArtistPictures:MaxUploadBytes"] = "2048",
            ["ArtistPictures:RequestTimeout"] = "00:00:05",
        });

        options.RootPath.Should().Be(Path.Combine(data.Path, "artist-pictures"));
        options.MaxUploadBytes.Should().Be(2048);
        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [TestCase("ArtistPictures:MaxUploadBytes", "0")]
    [TestCase("ArtistPictures:MaxDownloadBytes", "0")]
    [TestCase("ArtistPictures:MaxJsonBytes", "-1")]
    [TestCase("ArtistPictures:LookupCacheDuration", "00:00:00")]
    [TestCase("ArtistPictures:RequestTimeout", "-00:00:01")]
    public void AddWaveMpArtistPictures_InvalidSetting_FailsWhenResolved(string key, string value)
    {
        Action resolve = () => Resolve(new() { [key] = value });

        resolve.Should().Throw<OptionsValidationException>();
    }

    private static ArtistPictureOptions Resolve(Dictionary<string, string?> settings)
    {
        using ServiceProvider provider = CreateServices(settings).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ArtistPictureOptions>>().Value;
    }

    private static ServiceCollection CreateServices(Dictionary<string, string?> settings)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddScoped(_ => Substitute.For<IArtistPictureRepository>());
        services.AddWaveMpCaching();
        services.AddWaveMpArtistPictures();

        // The cover cache needs the settings of the cache; its own tests cover it.
        services.AddSingleton(Substitute.For<ICoverImageCache>());
        return services;
    }
}
