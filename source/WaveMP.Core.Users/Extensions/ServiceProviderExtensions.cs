using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Users.Avatars;
using WaveMP.Core.Users.Pins;
using WaveMP.Core.Users.Users;

namespace WaveMP.Core.Users.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Creates the built-in admin profile if it does not exist yet and applies <c>ADMIN_PIN</c> to it. Runs on start,
    /// after the migrations.
    /// </summary>
    public static async Task InitializeWaveMpUsersAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminProfileInitializer>().EnsureAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<AdminPinInitializer>().ApplyAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the folder of the uploaded avatars, removes unfinished files and the pictures of profiles that no
    /// longer exist or no longer use them. Runs on start, after the migrations. A folder that cannot be written does
    /// not stop the start: uploads are refused, and the reason is logged.
    /// </summary>
    public static async Task InitializeWaveMpAvatarsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<IAvatarStore>().InitializeAsync(cancellationToken);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAvatarService>().RemoveOrphansAsync(cancellationToken);
    }
}
