using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Scanning.Upgrades;

namespace WaveMP.Core.Scanning.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Carries out the changes of the library that the migrations left to the service (see
    /// <see cref="NameKeyUpgrader"/>). Call it right after the migrations, before anything reads the library.
    /// </summary>
    public static async Task UpgradeWaveMpLibraryAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NameKeyUpgrader>().RunAsync(cancellationToken);
    }
}
