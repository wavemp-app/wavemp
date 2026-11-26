using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WaveMP.Core.ArtistPictures.Lookups;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.ArtistPictures.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the artist pictures: the picture in the artist folder (delivered through the cover cache), uploaded
    /// pictures and pictures taken over from the online lookup in the folder <c>artist-pictures</c> of the data folder
    /// (<c>DATA_PATH</c>, default <c>/data</c>), with settings from the section <c>ArtistPictures</c>, validated when
    /// they are first resolved, and the online lookup with its answers in the cache area <c>artist-lookups</c>. Needs
    /// <c>AddWaveMpData</c>, <c>AddWaveMpSettings</c>, <c>AddWaveMpCaching</c> (ffmpeg converts the pictures) and
    /// <c>AddWaveMpOnlineMetadata</c> (the clients of the services). The folder is prepared on start by
    /// <see cref="ServiceProviderExtensions.InitializeWaveMpArtistPicturesAsync"/>.
    /// </summary>
    public static IServiceCollection AddWaveMpArtistPictures(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ArtistPictureOptions>()
            .BindConfiguration(ArtistPictureOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    ArtistPictureOptions.FolderName))
            .Validate(
                options => options.MaxUploadBytes >= 1
                    && options.MaxDownloadBytes >= 1
                    && options.MaxJsonBytes >= 1,
                "ArtistPictures:MaxUploadBytes, MaxDownloadBytes and MaxJsonBytes must be at least 1.")
            .Validate(
                options => options.LookupCacheDuration > TimeSpan.Zero && options.RequestTimeout > TimeSpan.Zero,
                "ArtistPictures:LookupCacheDuration and RequestTimeout must be longer than zero.");

        // The limits of the requests of the online lookup come from the same section.
        services.AddOptions<ArtistLookupClientOptions>()
            .Configure<IOptions<ArtistPictureOptions>>((client, pictures) =>
            {
                client.MaxJsonBytes = pictures.Value.MaxJsonBytes;
                client.MaxDownloadBytes = pictures.Value.MaxDownloadBytes;
                client.RequestTimeout = pictures.Value.RequestTimeout;
            });

        services.AddSingleton<IArtistPictureStore, ArtistPictureStore>();
        services.AddScoped<IArtistPictureService, ArtistPictureService>();
        services.AddScoped<IAlbumCoverFallback, ArtistPictureAlbumCoverFallback>();
        services.AddSingleton<ArtistLookupCache>();
        services.AddScoped<IArtistPictureLookupService, ArtistPictureLookupService>();

        return services;
    }
}
