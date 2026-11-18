using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Services;

/// <summary>
/// The result of installing or replacing a theme.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Theme">The installed theme, if it was installed or replaced.</param>
/// <param name="Errors">Why the package is rejected, with <see cref="ThemeInstallStatus.Invalid"/>.</param>
public sealed record ThemeInstallResult(
    ThemeInstallStatus Status,
    InstalledTheme? Theme = null,
    IReadOnlyList<ThemeError>? Errors = null);
