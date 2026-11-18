using WaveMP.Core.Themes.Storage;

namespace WaveMP.Core.Themes.Services;

/// <summary>
/// The uploaded themes: installing, replacing and deleting them (for admins) and listing them (for every profile).
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// The installed themes by name.
    /// </summary>
    IReadOnlyList<InstalledTheme> List();

    InstalledTheme? Find(string id);

    /// <summary>
    /// Checks a theme package (a ZIP file) and installs it as a new theme.
    /// </summary>
    Task<ThemeInstallResult> InstallAsync(Stream package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a theme package and replaces the installed theme <paramref name="id"/> with it; the id of its manifest
    /// must be the same. Profiles that chose the theme keep it.
    /// </summary>
    Task<ThemeInstallResult> ReplaceAsync(string id, Stream package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an installed theme; profiles that chose it get the default theme. <c>false</c> if there is no
    /// installed theme with the id.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
