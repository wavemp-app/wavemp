using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.ArtistPictures.Stores;

namespace WaveMP.Core.ArtistPictures.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Creates the folder of the stored artist pictures, removes unfinished files and the files of pictures that no
    /// longer exist (e.g. of artists a scan removed). Runs on start, after the migrations. A folder that cannot be
    /// written does not stop the start: new pictures are refused, and the reason is logged.
    /// </summary>
    public static async Task InitializeWaveMpArtistPicturesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<IArtistPictureStore>().InitializeAsync(cancellationToken);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IArtistPictureService>().RemoveOrphansAsync(cancellationToken);
    }
}
