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
    /// Registers the online search for album info: the clients of MusicBrainz and the Cover Art Archive (settings from
    /// the section <c>OnlineMetadata</c> and the settings group <c>online-metadata</c>), the search and the applying of
    /// matches. Needs <c>AddWaveMpData</c>, <c>AddWaveMpSettings</c>, <c>AddWaveMpMetadata</c>,
    /// <c>AddWaveMpScanning</c> and <c>AddWaveMpLibrary</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpOnlineMetadata(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OnlineMetadataOptions>().BindConfiguration(OnlineMetadataOptions.SectionName);

        services.AddSingleton<MusicBrainzRequestQueue>();
        services.AddSingleton(provider => new OnlineRequestSender(
            CreateHttpClient(provider.GetRequiredService<IOptions<OnlineMetadataOptions>>().Value),
            provider.GetRequiredService<MusicBrainzRequestQueue>(),
            provider.GetRequiredService<ISettingsSnapshot<OnlineMetadataSettings>>(),
            provider.GetRequiredService<IOptions<OnlineMetadataOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OnlineRequestSender>>()));
        services.AddSingleton<IMusicBrainzClient, MusicBrainzClient>();
        services.AddSingleton<ICoverArtArchiveClient, CoverArtArchiveClient>();

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
}
