using Microsoft.Extensions.DependencyInjection;
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
}
