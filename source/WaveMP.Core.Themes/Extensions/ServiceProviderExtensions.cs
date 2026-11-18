using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Themes.Storage;

namespace WaveMP.Core.Themes.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Prepares the folder of the themes and reads the installed themes. Without a writable folder WaveMP still
    /// starts; themes then cannot be installed.
    /// </summary>
    public static Task InitializeWaveMpThemesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<IThemeStore>().InitializeAsync(cancellationToken);
    }
}
