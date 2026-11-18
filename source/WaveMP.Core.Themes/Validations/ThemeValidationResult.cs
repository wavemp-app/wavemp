namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// The result of checking a theme package: the theme, or the reasons why it is rejected.
/// </summary>
public sealed class ThemeValidationResult
{
    public required ValidatedTheme? Theme { get; init; }

    public required IReadOnlyList<ThemeError> Errors { get; init; }
}
