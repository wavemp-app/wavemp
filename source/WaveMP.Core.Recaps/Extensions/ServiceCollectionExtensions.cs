using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Recaps.Options;
using WaveMP.Core.Recaps.Shares;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Core.Recaps.Videos;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Recaps.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the statistics of the play history, the recaps shared with the other profiles and the conversion of
    /// recorded recap videos to MP4 in the folder <c>temp/recap-videos</c> of the data folder (<c>DATA_PATH</c>,
    /// default <c>/data</c>), which is cleaned up in the background, with settings from the section <c>Recaps</c>.
    /// Needs <c>AddWaveMpData</c> and <c>AddWaveMpTranscoding</c> (ffmpeg).
    /// </summary>
    public static IServiceCollection AddWaveMpRecaps(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RecapOptions>()
            .BindConfiguration(RecapOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.VideoPath = Path.Combine(
                    DataPaths.Resolve(configuration, provider.GetService<IHostEnvironment>(), RecapOptions.FolderName),
                    RecapOptions.VideoFolderName));

        services.AddScoped<IListeningStatisticsService, ListeningStatisticsService>();
        services.AddScoped<IRecapShareService, RecapShareService>();
        services.AddSingleton<IRecapVideoConverter, RecapVideoConverter>();
        services.AddSingleton<RecapVideoCleaner>();
        services.AddHostedService<RecapVideoJanitor>();

        return services;
    }
}
