using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Settings.Paths;
using WaveMP.Core.Users.Avatars;
using WaveMP.Core.Users.Pins;
using WaveMP.Core.Users.Sessions;
using WaveMP.Core.Users.Settings;
using WaveMP.Core.Users.Users;

namespace WaveMP.Core.Users.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers profiles, sessions, PINs, avatars and user settings. Needs the repositories of <c>AddWaveMpData</c>
    /// and, for uploaded avatars, <c>AddWaveMpCaching</c> (ffmpeg converts the pictures). The PIN of the built-in admin
    /// profile comes from <c>ADMIN_PIN</c>, the limits of wrong PINs from the section <c>Pins</c>; both are validated
    /// when the options are first resolved. Uploaded avatars lie in the folder <c>avatars</c> of the data folder
    /// (<c>DATA_PATH</c>, default <c>/data</c>), with settings from the section <c>Avatars</c>; the folder is prepared
    /// on start by <see cref="ServiceProviderExtensions.InitializeWaveMpAvatarsAsync"/>.
    /// </summary>
    public static IServiceCollection AddWaveMpUsers(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<SessionOptions>();
        services.AddOptions<PinOptions>()
            .BindConfiguration(PinOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.AdminPin = configuration[PinOptions.AdminPinVariable] is { Length: > 0 } pin ? pin : null)
            .Validate(
                options => options.AdminPin is null || PinRules.IsValid(options.AdminPin),
                $"{PinOptions.AdminPinVariable} must consist of {PinRules.MinLength} to {PinRules.MaxLength} digits.")
            .Validate(
                options => options.MaxFailedAttempts >= 1 && options.LockoutDuration > TimeSpan.Zero,
                "Pins:MaxFailedAttempts must be at least 1 and Pins:LockoutDuration longer than zero.");
        services.AddOptions<AvatarOptions>()
            .BindConfiguration(AvatarOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    AvatarOptions.FolderName))
            .Validate(options => options.MaxUploadBytes >= 1, "Avatars:MaxUploadBytes must be at least 1.");

        services.AddSingleton<IPinHasher, PinHasher>();
        services.AddSingleton<IAvatarStore, AvatarStore>();
        services.AddScoped<IPinService, PinService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAvatarService, AvatarService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<AdminProfileInitializer>();
        services.AddScoped<AdminPinInitializer>();

        return services;
    }
}
