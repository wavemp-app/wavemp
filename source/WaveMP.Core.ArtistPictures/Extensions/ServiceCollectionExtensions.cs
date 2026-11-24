using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.ArtistPictures.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the artist pictures: the picture in the artist folder (delivered through the cover cache), uploaded
    /// pictures and pictures taken over from the online lookup in the folder <c>artist-pictures</c> of the data folder
    /// (<c>DATA_PATH</c>, default <c>/data</c>), with settings from the section <c>ArtistPictures</c>, validated when
    /// they are first resolved. Needs <c>AddWaveMpData</c> and <c>AddWaveMpCaching</c> (ffmpeg converts the pictures).
    /// The folder is prepared on start by <see cref="ServiceProviderExtensions.InitializeWaveMpArtistPicturesAsync"/>.
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

        services.AddSingleton<IArtistPictureStore, ArtistPictureStore>();
        services.AddScoped<IArtistPictureService, ArtistPictureService>();

        return services;
    }
}
