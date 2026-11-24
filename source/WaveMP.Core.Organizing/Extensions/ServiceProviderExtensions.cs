using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.OwnAlbums;

namespace WaveMP.Core.Organizing.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Creates the folder of the own album covers and removes unfinished uploads and covers of albums that no longer
    /// exist, then puts back what an organize run left half done when the server stopped (see
    /// <see cref="OrganizeRecovery"/>) and removes old previews. Runs on start, after the migrations and after the
    /// media sources are loaded, before the jobs start. A folder that cannot be written does not stop the start.
    /// </summary>
    public static async Task InitializeWaveMpOrganizingAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<IOwnAlbumCoverStore>().InitializeAsync(cancellationToken);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOwnAlbumService>().RemoveOrphanCoversAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<OrganizeRecovery>().RecoverAsync(cancellationToken);
    }
}
