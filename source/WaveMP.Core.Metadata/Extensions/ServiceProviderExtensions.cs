using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WaveMP.Core.Metadata.Sources;

namespace WaveMP.Core.Metadata.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Loads the media sources and warns about active sources whose folder does not exist. Runs on start, after the
    /// migrations and before the file watcher and the jobs start.
    /// </summary>
    public static async Task InitializeWaveMpMediaSourcesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        IMediaSourceCatalog catalog = services.GetRequiredService<IMediaSourceCatalog>();
        await catalog.RefreshAsync(cancellationToken);

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MediaSourceInfo));
        foreach (MediaSourceInfo source in catalog.Sources.Where(source => source.IsActive))
        {
            if (Directory.Exists(source.RootPath))
            {
                continue;
            }

            if (source.FollowsMediaPath)
            {
                logger.LogWarning(
                    "The folder {Folder} of the media source {Source} does not exist; mount it or set MEDIA_PATH",
                    source.RootPath,
                    source.Name);
            }
            else
            {
                logger.LogWarning(
                    "The folder {Folder} of the media source {Source} does not exist; mount it",
                    source.RootPath,
                    source.Name);
            }
        }
    }
}
