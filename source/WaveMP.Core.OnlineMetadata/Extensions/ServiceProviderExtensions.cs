using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Removes the pictures of online covers whose album or song is gone (the database removes their rows with them).
    /// Runs on start, after the migrations; a folder that cannot be read only logs a warning.
    /// </summary>
    public static async Task InitializeWaveMpOnlineMetadataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IOnlineCoverFiles files = scope.ServiceProvider.GetRequiredService<IOnlineCoverFiles>();
        IReadOnlyList<string> stored = files.ListFileNames();
        if (stored.Count == 0)
        {
            return;
        }

        IReadOnlySet<string> known = await scope.ServiceProvider.GetRequiredService<IOnlineMetadataRepository>()
            .ListOnlineCoverFileNamesAsync(cancellationToken);
        string[] orphans = [.. stored.Where(name => !known.Contains(name))];
        foreach (string name in orphans)
        {
            files.Delete(name);
        }

        if (orphans.Length > 0)
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("WaveMP.Core.OnlineMetadata")
                .LogInformation("Removed {Count} online covers whose album or song is gone", orphans.Length);
        }
    }
}
