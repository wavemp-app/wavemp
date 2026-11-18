namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// A file of a checked theme: its installed name (see <c>ThemeFiles</c>) and its checked content.
/// </summary>
public sealed record ValidatedThemeFile(string Name, byte[] Content);
