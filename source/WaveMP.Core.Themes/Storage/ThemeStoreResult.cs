namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// The result of installing a theme in the store.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Theme">The installed theme, if it was installed.</param>
public sealed record ThemeStoreResult(ThemeStoreStatus Status, InstalledTheme? Theme = null);
