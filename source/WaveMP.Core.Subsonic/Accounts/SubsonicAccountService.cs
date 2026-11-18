using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Accounts;

internal sealed class SubsonicAccountService(
    ISubsonicRepository repository,
    ISubsonicPasswordProtector protector,
    TimeProvider timeProvider) : ISubsonicAccountService
{
    public async Task<SubsonicAccountState> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return ToState(await repository.FindAccountAsync(userId, cancellationToken));
    }

    public async Task<SubsonicAccountState> SetEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        return ToState(await repository.SetEnabledAsync(userId, enabled, cancellationToken));
    }

    public async Task<SubsonicPassword> CreatePasswordAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string password = SubsonicPasswords.Generate();
        DateTimeOffset createdAt = timeProvider.GetUtcNow();
        SubsonicAccount account = await repository.SetPasswordAsync(
            userId,
            protector.Protect(password),
            SubsonicPasswords.Hash(password),
            createdAt,
            cancellationToken);
        return new SubsonicPassword(password, createdAt, ToState(account));
    }

    public async Task<SubsonicAccountState> RevokePasswordAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await repository.RemovePasswordAsync(userId, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    private static SubsonicAccountState ToState(SubsonicAccount? account)
    {
        return account is null
            ? SubsonicAccountState.Off
            : new SubsonicAccountState(
                account.Enabled,
                account.ProtectedPassword is not null,
                account.ProtectedPassword is null ? null : account.PasswordCreatedAt);
    }
}
