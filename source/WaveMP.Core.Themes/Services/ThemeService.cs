using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Packages;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Themes.Services;

internal sealed class ThemeService(
    IThemeStore store,
    IUserRepository users,
    IOptions<ThemeOptions> options,
    ILogger<ThemeService> logger) : IThemeService
{
    private const int ZipHeaderLength = 4;

    public IReadOnlyList<InstalledTheme> List()
    {
        return store.List();
    }

    public InstalledTheme? Find(string id)
    {
        return store.Find(id);
    }

    public Task<ThemeInstallResult> InstallAsync(Stream package, CancellationToken cancellationToken = default)
    {
        return SaveAsync(null, package, cancellationToken);
    }

    public Task<ThemeInstallResult> ReplaceAsync(
        string id,
        Stream package,
        CancellationToken cancellationToken = default)
    {
        return store.Find(id) is null
            ? Task.FromResult(new ThemeInstallResult(ThemeInstallStatus.NotFound))
            : SaveAsync(id, package, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await store.DeleteAsync(id, cancellationToken))
        {
            return false;
        }

        int changed = await users.ResetThemeAsync(id, cancellationToken);
        logger.LogInformation(
            "Deleted the theme {ThemeId}; {Count} profiles got the default theme",
            id,
            changed);
        return true;
    }

    private async Task<ThemeInstallResult> SaveAsync(
        string? replacedId,
        Stream package,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[ZipHeaderLength];
        package.Position = 0;
        int read = await package.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (!ThemePackageReader.LooksLikeZip(header.AsSpan(0, read)))
        {
            return new ThemeInstallResult(ThemeInstallStatus.UnsupportedFormat);
        }

        package.Position = 0;
        ThemeValidationResult validation = ThemePackageValidator.Validate(package, options.Value, replacedId);
        if (validation.Theme is not { } theme)
        {
            return new ThemeInstallResult(ThemeInstallStatus.Invalid, Errors: validation.Errors);
        }

        ThemeStoreResult result = await store.SaveAsync(theme, replace: replacedId is not null, cancellationToken);
        return result.Status switch
        {
            ThemeStoreStatus.Saved when replacedId is null => Saved(ThemeInstallStatus.Installed, result.Theme!),
            ThemeStoreStatus.Saved => Saved(ThemeInstallStatus.Replaced, result.Theme!),
            ThemeStoreStatus.AlreadyInstalled => new ThemeInstallResult(ThemeInstallStatus.AlreadyInstalled),
            ThemeStoreStatus.NotFound => new ThemeInstallResult(ThemeInstallStatus.NotFound),
            _ => new ThemeInstallResult(ThemeInstallStatus.Unavailable),
        };
    }

    private ThemeInstallResult Saved(ThemeInstallStatus status, InstalledTheme theme)
    {
        logger.LogInformation(
            "{Action} the theme {ThemeId} {Version}",
            status == ThemeInstallStatus.Installed ? "Installed" : "Replaced",
            theme.Id,
            theme.Version);
        return new ThemeInstallResult(status, theme);
    }
}
