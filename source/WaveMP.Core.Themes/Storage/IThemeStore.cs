using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// The installed themes in the folder of the themes (<c>/data/themes</c>): one folder per theme with its
/// <c>theme.json</c> and its files. The store keeps the list in memory; it is read on start.
/// </summary>
public interface IThemeStore
{
    /// <summary>
    /// Whether the folder could be prepared; without it, themes cannot be installed.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Prepares the folder, removes the remains of interrupted installations, reads the installed themes and checks
    /// them again with the rules of this version (<see cref="InstalledTheme.Problems"/>).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The installed themes by name.
    /// </summary>
    IReadOnlyList<InstalledTheme> List();

    InstalledTheme? Find(string id);

    /// <summary>
    /// Installs a checked theme. With <paramref name="replace"/> it replaces the installed theme of the same id
    /// (which has to exist), otherwise the id must be new.
    /// </summary>
    Task<ThemeStoreResult> SaveAsync(
        ValidatedTheme theme,
        bool replace,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an installed theme with its files; <c>false</c> if there is none with the id.
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file of an installed theme for reading, or returns <c>null</c> if the theme or the file does not exist
    /// or the theme is blocked (<see cref="InstalledTheme.IsBlocked"/>).
    /// </summary>
    Stream? OpenFile(string id, string name);
}
