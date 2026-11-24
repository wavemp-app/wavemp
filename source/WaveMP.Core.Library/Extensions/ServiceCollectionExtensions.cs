using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Checks;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Library.Groups;
using WaveMP.Core.Library.Options;
using WaveMP.Core.Library.Reviews;
using WaveMP.Core.Library.Searches;
using WaveMP.Core.Library.Tracks;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Library.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the library: artists, albums, tracks, genres, years, the groups of their lists, the search, the
    /// covers and their rules (with the online covers in the folder <c>online-covers</c> of the data folder,
    /// <c>DATA_PATH</c>, default <c>/data</c>), the review status of the tracks and the library check with its job <c>library-check</c> (settings from the
    /// section <c>LibraryCheck</c>), which the scan starts after every run. Needs the repositories of
    /// <c>AddWaveMpData</c>, the library settings of <c>AddWaveMpSettings</c>, the media folder of
    /// <c>AddWaveMpMetadata</c> and the jobs of <c>AddWaveMpJobs</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpLibrary(this IServiceCollection services)
    {
        services.AddOptions<OnlineCoverOptions>()
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    OnlineCoverOptions.FolderName));
        services.AddOptions<LibraryCheckOptions>().BindConfiguration(LibraryCheckOptions.SectionName);

        services.AddScoped<IArtistService, ArtistService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<ITrackService, TrackService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IListGroupService, ListGroupService>();
        services.AddScoped<ICoverService, CoverService>();
        services.AddScoped<ICoverRuleService, CoverRuleService>();
        services.AddScoped<ITrackReviewService, TrackReviewService>();
        services.AddScoped<ILibraryIssueService, LibraryIssueService>();
        services.AddScoped<ILibraryCheckRunner, LibraryCheckRunner>();
        services.AddSingleton<IOnlineCoverFiles, OnlineCoverFiles>();
        services.AddSingleton<ICoverSourceReader, CoverSourceReader>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddWaveMpJob<LibraryCheckJob>(JobIds.LibraryCheck);

        return services;
    }
}
