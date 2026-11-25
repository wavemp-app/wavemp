using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Core.Subsonic.Annotations;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Browsing;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.Media;
using WaveMP.Core.Subsonic.NowPlaying;
using WaveMP.Core.Subsonic.Playlists;

namespace WaveMP.Core.Subsonic.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Subsonic API: the Subsonic access and app passwords of the profiles (encrypted with Data
    /// Protection), the sign-in of the apps, browsing, playlists, favorites, ratings, plays, "Now Playing", streams
    /// with their transcodes and covers. Needs <c>AddWaveMpData</c>, <c>AddWaveMpLibrary</c>,
    /// <c>AddWaveMpCollection</c>, <c>AddWaveMpPlaylists</c>, <c>AddWaveMpCaching</c>, <c>AddWaveMpStreaming</c>,
    /// <c>AddWaveMpScrobbling</c>, <c>AddWaveMpTranscoding</c>, <c>AddWaveMpSettings</c> and Data Protection.
    /// </summary>
    public static IServiceCollection AddWaveMpSubsonic(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISubsonicPasswordProtector, SubsonicPasswordProtector>();
        services.AddSingleton<ISubsonicNowPlayingRegistry, SubsonicNowPlayingRegistry>();
        services.AddSingleton<ISubsonicTranscoder, SubsonicTranscoder>();

        services.AddScoped<ISubsonicAccountService, SubsonicAccountService>();
        services.AddScoped<ISubsonicAuthenticator, SubsonicAuthenticator>();
        services.AddScoped<SubsonicElementLoader>();
        services.AddScoped<SubsonicMusicFolders>();
        services.AddScoped<ISubsonicBrowsingService, SubsonicBrowsingService>();
        services.AddScoped<ISubsonicPlaylistService, SubsonicPlaylistService>();
        services.AddScoped<ISubsonicAnnotationService, SubsonicAnnotationService>();
        services.AddScoped<ISubsonicMediaService, SubsonicMediaService>();

        return services;
    }
}
