using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.OnlineMetadata.Searches;
using WaveMP.Core.OnlineMetadata.Updates;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The key of the <see cref="HttpMessageHandler"/> of the requests of the online lookup of artist pictures (a keyed
    /// singleton), e.g. for tests that replace it with simulated services.
    /// </summary>
    public const string ArtistLookupHandlerKey = "artist-lookups";

    /// <summary>
    /// Registers the online search for album info: the clients of MusicBrainz and the Cover Art Archive (settings from
    /// the section <c>OnlineMetadata</c> and the settings group <c>online-metadata</c>), the search and the applying of
    /// matches; and the clients of the online lookup of artist pictures (MusicBrainz artists, Wikidata, Wikimedia
    /// Commons, fanart.tv), which depend on the settings group <c>artist-pictures</c>. Needs <c>AddWaveMpData</c>,
    /// <c>AddWaveMpSettings</c>, <c>AddWaveMpMetadata</c>, <c>AddWaveMpScanning</c> and <c>AddWaveMpLibrary</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpOnlineMetadata(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OnlineMetadataOptions>().BindConfiguration(OnlineMetadataOptions.SectionName);
        services.AddOptions<ArtistLookupClientOptions>();

        // Redirects of the artist picture lookup are followed by the sender, only to allowed hosts.
        services.TryAddKeyedSingleton<HttpMessageHandler>(
            ArtistLookupHandlerKey,
            (_, _) => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                UseCookies = false,
            });

        services.AddSingleton<MusicBrainzRequestQueue>();
        services.AddSingleton(provider => new OnlineRequestSender(
            CreateHttpClient(provider.GetRequiredService<IOptions<OnlineMetadataOptions>>().Value),
            CreateArtistLookupClient(provider.GetRequiredKeyedService<HttpMessageHandler>(ArtistLookupHandlerKey)),
            provider.GetRequiredService<MusicBrainzRequestQueue>(),
            provider.GetRequiredService<ISettingsSnapshot<OnlineMetadataSettings>>(),
            provider.GetRequiredService<ISettingsSnapshot<ArtistPictureSettings>>(),
            provider.GetRequiredService<IOptions<OnlineMetadataOptions>>(),
            provider.GetRequiredService<IOptions<ArtistLookupClientOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OnlineRequestSender>>()));
        services.AddSingleton<IMusicBrainzClient, MusicBrainzClient>();
        services.AddSingleton<ICoverArtArchiveClient, CoverArtArchiveClient>();
        services.AddSingleton<IMusicBrainzArtistClient, MusicBrainzArtistClient>();
        services.AddSingleton<IWikidataClient, WikidataClient>();
        services.AddSingleton<IWikimediaCommonsClient, WikimediaCommonsClient>();
        services.AddSingleton<IFanartTvClient, FanartTvClient>();

        services.AddScoped<IOnlineMetadataSearchService, OnlineMetadataSearchService>();
        services.AddScoped<IOnlineCoverWriter, OnlineCoverWriter>();
        services.AddScoped<IOnlineMetadataApplier, OnlineMetadataApplier>();

        return services;
    }

    /// <summary>
    /// The HTTP client of both services for the lifetime of the service (the requests carry the User-Agent of
    /// <see cref="OnlineUserAgent"/>); connections are renewed regularly, so changes of DNS apply. It follows the
    /// redirects of the Cover Art Archive to the pictures.
    /// </summary>
    private static HttpClient CreateHttpClient(OnlineMetadataOptions options)
    {
        SocketsHttpHandler handler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
        };
        return new HttpClient(handler) { Timeout = options.RequestTimeout };
    }

    /// <summary>
    /// The HTTP client of the artist picture lookup over the registered handler, which it does not own. The sender
    /// limits the time of every request itself.
    /// </summary>
    private static HttpClient CreateArtistLookupClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
