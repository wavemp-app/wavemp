using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Library.Options;
using WaveMP.Core.Library.Reviews;
using WaveMP.Core.Library.Searches;
using WaveMP.Core.Library.Tracks;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Library.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the library: artists, albums, tracks, genres, years, the search, the covers and their rules (with the
    /// online covers in the folder <c>online-covers</c> of the data folder, <c>DATA_PATH</c>, default <c>/data</c>)
    /// and the review status of the tracks. Needs the repositories of <c>AddWaveMpData</c>, the library settings of
    /// <c>AddWaveMpSettings</c> and the media folder of <c>AddWaveMpMetadata</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpLibrary(this IServiceCollection services)
    {
        services.AddOptions<OnlineCoverOptions>()
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    OnlineCoverOptions.FolderName));

        services.AddScoped<IArtistService, ArtistService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<ITrackService, TrackService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ICoverService, CoverService>();
        services.AddScoped<ICoverRuleService, CoverRuleService>();
        services.AddScoped<ITrackReviewService, TrackReviewService>();
        services.AddSingleton<IOnlineCoverFiles, OnlineCoverFiles>();
        services.AddSingleton<ICoverSourceReader, CoverSourceReader>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
