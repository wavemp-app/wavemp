using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Settings.Paths;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Services;
using WaveMP.Core.Themes.Storage;

namespace WaveMP.Core.Themes.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the uploaded themes. Needs the repositories of <c>AddWaveMpData</c> (profiles that chose a deleted
    /// theme get the default theme). The themes lie in the folder <c>themes</c> of the data folder (<c>DATA_PATH</c>,
    /// default <c>/data</c>), with limits from the section <c>Themes</c>; the folder is prepared on start by
    /// <see cref="ServiceProviderExtensions.InitializeWaveMpThemesAsync"/>.
    /// </summary>
    public static IServiceCollection AddWaveMpThemes(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ThemeOptions>()
            .BindConfiguration(ThemeOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    ThemeOptions.FolderName))
            .Validate(
                options => options.MaxUploadBytes >= 1 && options.MaxUnpackedBytes >= 1 && options.MaxFileBytes >= 1 &&
                    options.MaxStylesheetBytes >= 1 && options.MaxFontBytes >= 1 && options.MaxEntries >= 1,
                "The limits of the section Themes must be at least 1.");

        services.AddSingleton<IThemeStore, ThemeStore>();
        services.AddScoped<IThemeService, ThemeService>();
        return services;
    }
}
