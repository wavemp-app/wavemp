using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Jobs;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;

namespace WaveMP.Core.Lyrics.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the lyrics: reading them from lyrics files, the database and tags, the download from LRCLIB (settings
    /// from the section <c>Lyrics</c>) and the job <c>lyrics-download</c>, which is started after scans and when the
    /// download is switched on. Needs <c>AddWaveMpData</c>, <c>AddWaveMpSettings</c>, <c>AddWaveMpMetadata</c>,
    /// <c>AddWaveMpJobs</c> and <c>AddWaveMpScanning</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpLyrics(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<LyricsOptions>().BindConfiguration(LyricsOptions.SectionName);

        services.AddSingleton<ILyricsFileStore, LyricsFileStore>();
        services.AddScoped<ILyricsService, LyricsService>();

        services.AddSingleton<LrclibRequestPacer>();
        services.AddSingleton<ILrclibClient>(provider => new LrclibClient(
            CreateHttpClient(provider.GetRequiredService<IOptions<LyricsOptions>>().Value),
            provider.GetRequiredService<LrclibRequestPacer>(),
            provider.GetRequiredService<ILogger<LrclibClient>>()));
        services.AddScoped<ILyricsDownloader, LyricsDownloader>();
        services.AddScoped<ILyricsDownloadRunner, LyricsDownloadRunner>();

        services.AddWaveMpJob<LyricsDownloadJob>(JobIds.LyricsDownload);

        // Scoped like the settings service that calls it, because starting a job needs the database.
        services.AddScoped<ISettingsChangeHandler<LibrarySettings>, LyricsDownloadSettingsHandler>();

        return services;
    }

    /// <summary>
    /// The HTTP client of LRCLIB for the lifetime of the service; connections are renewed regularly, so changes of
    /// DNS apply.
    /// </summary>
    private static HttpClient CreateHttpClient(LyricsOptions options)
    {
        SocketsHttpHandler handler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
        };
        HttpClient client = new(handler)
        {
            BaseAddress = options.LrclibUrl,
            Timeout = options.RequestTimeout,
        };
        foreach (System.Net.Http.Headers.ProductInfoHeaderValue product in LrclibClient.UserAgent)
        {
            client.DefaultRequestHeaders.UserAgent.Add(product);
        }

        return client;
    }
}
