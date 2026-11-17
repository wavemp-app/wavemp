using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Library.Reviews;
using WaveMP.Core.Library.Searches;
using WaveMP.Core.Library.Tracks;

namespace WaveMP.Core.Library.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the library: artists, albums, tracks, genres, years, the search, the covers and their rules and the
    /// review status of the tracks. Needs the repositories of <c>AddWaveMpData</c>, the library settings of
    /// <c>AddWaveMpSettings</c> and the media folder of <c>AddWaveMpMetadata</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpLibrary(this IServiceCollection services)
    {
        services.AddScoped<IArtistService, ArtistService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<ITrackService, TrackService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ICoverService, CoverService>();
        services.AddScoped<ICoverRuleService, CoverRuleService>();
        services.AddScoped<ITrackReviewService, TrackReviewService>();
        services.AddSingleton<ICoverSourceReader, CoverSourceReader>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
