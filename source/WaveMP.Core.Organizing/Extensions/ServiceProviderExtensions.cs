using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Organizing.OwnAlbums;

namespace WaveMP.Core.Organizing.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Creates the folder of the own album covers and removes unfinished uploads and covers of albums that no longer
    /// exist. Runs on start, after the migrations. A folder that cannot be written does not stop the start.
    /// </summary>
    public static async Task InitializeWaveMpOrganizingAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<IOwnAlbumCoverStore>().InitializeAsync(cancellationToken);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOwnAlbumService>().RemoveOrphanCoversAsync(cancellationToken);
    }
}
