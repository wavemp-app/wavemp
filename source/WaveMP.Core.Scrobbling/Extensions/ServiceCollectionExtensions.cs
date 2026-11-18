using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Core.Scrobbling.Credentials;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Core.Streaming.Histories;

namespace WaveMP.Core.Scrobbling.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scrobbling to Last.fm and ListenBrainz: the connections of the profiles, "Now Playing", the queue
    /// of scrobbles in the database with its sender in the background, and the listener that queues every play of the
    /// history. Settings come from the section <c>Scrobbling</c>, the API account of Last.fm from
    /// <c>LASTFM_API_KEY</c> and <c>LASTFM_SHARED_SECRET</c>. Needs <c>AddWaveMpData</c>, <c>AddWaveMpStreaming</c>
    /// and Data Protection (the credentials are encrypted with it).
    /// </summary>
    public static IServiceCollection AddWaveMpScrobbling(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ScrobblingOptions>()
            .BindConfiguration(ScrobblingOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.LastFmApiKey = Variable(configuration, ScrobblingOptions.LastFmApiKeyVariable)
                    ?? options.LastFmApiKey;
                options.LastFmSharedSecret = Variable(configuration, ScrobblingOptions.LastFmSharedSecretVariable)
                    ?? options.LastFmSharedSecret;
            });

        services.AddSingleton<ILastFmClient>(provider =>
        {
            IOptions<ScrobblingOptions> options = provider.GetRequiredService<IOptions<ScrobblingOptions>>();
            return new LastFmClient(
                ScrobbleHttp.Create(options.Value.LastFmApiUrl, options.Value.RequestTimeout),
                options,
                provider.GetRequiredService<ILogger<LastFmClient>>());
        });
        services.AddSingleton<IListenBrainzClient>(provider =>
        {
            ScrobblingOptions options = provider.GetRequiredService<IOptions<ScrobblingOptions>>().Value;
            return new ListenBrainzClient(
                ScrobbleHttp.Create(options.ListenBrainzApiUrl, options.RequestTimeout),
                provider.GetRequiredService<ILogger<ListenBrainzClient>>());
        });
        services.AddSingleton<IScrobbleSender, ScrobbleSender>();
        services.AddSingleton<IScrobbleCredentialProtector, ScrobbleCredentialProtector>();
        services.AddSingleton<ScrobbleQueueSignal>();

        services.AddScoped<IScrobbleConnectionService, ScrobbleConnectionService>();
        services.AddScoped<IScrobbleQueue, ScrobbleQueue>();
        services.AddScoped<IScrobbleQueueProcessor, ScrobbleQueueProcessor>();
        services.AddScoped<INowPlayingService, NowPlayingService>();
        services.AddScoped<IPlayHistoryListener, ScrobblePlayListener>();
        services.AddHostedService<ScrobbleQueueWorker>();

        return services;
    }

    private static string? Variable(IConfiguration configuration, string name)
    {
        return configuration[name] is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }
}
