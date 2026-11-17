using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Users.Pins;
using WaveMP.Core.Users.Sessions;
using WaveMP.Core.Users.Settings;
using WaveMP.Core.Users.Users;

namespace WaveMP.Core.Users.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers profiles, sessions, PINs and user settings. Needs the repositories of <c>AddWaveMpData</c>. The PIN of
    /// the built-in admin profile comes from <c>ADMIN_PIN</c>, the limits of wrong PINs from the section <c>Pins</c>;
    /// both are validated when the options are first resolved.
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

        services.AddSingleton<IPinHasher, PinHasher>();
        services.AddScoped<IPinService, PinService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<AdminProfileInitializer>();
        services.AddScoped<AdminPinInitializer>();

        return services;
    }
}
