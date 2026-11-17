using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Applies <c>ADMIN_PIN</c> to the built-in admin profile on start: if the variable is set and the admin profile has
/// no PIN or another one, the PIN is set and all sessions of the admin profile end, so every device has to enter it.
/// Without the variable, the PIN of the admin profile stays as it is and can be changed in the app.
/// </summary>
public sealed class AdminPinInitializer(
    IUserRepository users,
    ISessionRepository sessions,
    IPinHasher hasher,
    IOptions<PinOptions> options,
    ILogger<AdminPinInitializer> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        string? pin = options.Value.AdminPin;
        if (string.IsNullOrEmpty(pin))
        {
            return;
        }

        if (!PinRules.IsValid(pin))
        {
            // The options are validated on start; this is the safety net.
            throw new InvalidOperationException("ADMIN_PIN must consist of 4 to 8 digits.");
        }

        User admin = await users.FindBuiltInAdminAsync(cancellationToken)
            ?? throw new InvalidOperationException("The built-in admin profile does not exist.");
        if (admin.PinHash is not null && hasher.Verify(admin.PinHash, pin))
        {
            return;
        }

        await users.SetPinHashAsync(admin.Id, hasher.Hash(pin), cancellationToken);
        int ended = await sessions.DeleteForUserAsync(admin.Id, cancellationToken: cancellationToken);
        logger.LogInformation(
            "Set the PIN of the built-in admin profile from ADMIN_PIN and ended its {Count} sessions",
            ended);
    }
}
