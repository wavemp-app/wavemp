using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the settings of the current profile. Missing or null values stay unchanged, also within the groups.
/// </summary>
public sealed class UpdateUserSettingsDto
{
    /// <summary>
    /// Language of the user interface as BCP 47 language tag, e.g. "en", "de" or "pt-BR" (language, optional script,
    /// region and variants; no extensions). The server keeps any valid tag in the usual case of its subtags
    /// ("PT-br" becomes "pt-BR"); clients offer the languages they have translations for.
    /// </summary>
    [MinLength(LanguageTags.MinLength)]
    [MaxLength(LanguageTags.MaxLength)]
    [RegularExpression(LanguageTags.Pattern)]
    public string? Language { get; init; }

    /// <summary>
    /// Whether the players even out the volume with the ReplayGain tags.
    /// </summary>
    public ReplayGainModeDto? ReplayGain { get; init; }

    /// <summary>
    /// What the Now Playing view shows.
    /// </summary>
    public PlayerViewModeDto? PlayerViewMode { get; init; }

    /// <summary>
    /// Whether the sidebar of the desktop layout shows only its icons.
    /// </summary>
    public bool? SidebarCollapsed { get; init; }

    /// <summary>
    /// Changes of the theme of the web app.
    /// </summary>
    public UpdateThemeSettingsDto? Theme { get; init; }

    /// <summary>
    /// Changes of the equalizer.
    /// </summary>
    public UpdateEqualizerSettingsDto? Equalizer { get; init; }

    /// <summary>
    /// Changes of the crossfade.
    /// </summary>
    public UpdateCrossfadeSettingsDto? Crossfade { get; init; }

    /// <summary>
    /// Changes of the sleep timer.
    /// </summary>
    public UpdateSleepTimerSettingsDto? SleepTimer { get; init; }

    /// <summary>
    /// Changes of the autoplay.
    /// </summary>
    public UpdateAutoplaySettingsDto? Autoplay { get; init; }

    /// <summary>
    /// Changes of the visualization.
    /// </summary>
    public UpdateVisualizationSettingsDto? Visualization { get; init; }
}
