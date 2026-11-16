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

    public static bool IsValidAutoChange(int seconds)
    {
        return seconds is >= AutoChangeMinSeconds and <= AutoChangeMaxSeconds;
    }

    public static bool IsValidPreset(string preset)
    {
        return preset.Length <= PresetMaxLength;
    }
}
