using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Settings;

/// <summary>
/// Limits of the visualization settings of a user.
/// </summary>
public static class VisualizationSettingsLimits
{
    /// <summary>
    /// Shortest interval of the automatic change of presets in seconds.
    /// </summary>
    public const int AutoChangeMinSeconds = 5;

    /// <summary>
    /// Longest interval of the automatic change of presets in seconds.
    /// </summary>
    public const int AutoChangeMaxSeconds = 600;

    /// <summary>
    /// Longest name of a preset.
    /// </summary>
    public const int PresetMaxLength = UserSettings.VisualizationPresetMaxLength;

    /// <summary>
    /// Longest id of a color scheme.
    /// </summary>
    public const int ColorSchemeMaxLength = UserSettings.VisualizationColorSchemeMaxLength;

    /// <summary>
    /// Slowest speed of the Milkdrop presets in percent.
    /// </summary>
    public const int MilkdropSpeedMin = 10;

    /// <summary>
    /// Fastest speed of the Milkdrop presets in percent, their original speed at 60 frames a second.
    /// </summary>
    public const int MilkdropSpeedMax = 100;

    /// <summary>
    /// Speed of the Milkdrop presets of new profiles in percent.
    /// </summary>
    public const int MilkdropSpeedDefault = UserSettings.DefaultVisualizationMilkdropSpeed;

    public static bool IsValidAutoChange(int seconds)
    {
        return seconds is >= AutoChangeMinSeconds and <= AutoChangeMaxSeconds;
    }

    public static bool IsValidPreset(string preset)
    {
        return preset.Length <= PresetMaxLength;
    }

    public static bool IsValidColorScheme(string colorScheme)
    {
        return colorScheme.Length <= ColorSchemeMaxLength;
    }

    public static bool IsValidMilkdropSpeed(int percent)
    {
        return percent is >= MilkdropSpeedMin and <= MilkdropSpeedMax;
    }
}
