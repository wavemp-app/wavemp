using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Settings;

/// <summary>
/// Changes the settings of a user. Every <c>null</c> value keeps the current value.
/// </summary>
/// <param name="Language">
/// Language of the user interface as BCP 47 language tag, see <see cref="LanguageTags"/>; it is saved in the usual case.
/// </param>
/// <param name="ReplayGain">Volume normalization of the player.</param>
/// <param name="PlayerViewMode">What the Now Playing stage shows.</param>
/// <param name="EqualizerEnabled">Whether the equalizer shapes the sound.</param>
/// <param name="EqualizerPreset">Curve of the equalizer.</param>
/// <param name="EqualizerPreampDb">Preamp of the equalizer in dB, see <see cref="PlaybackSettingsLimits"/>.</param>
/// <param name="EqualizerCustomGainsDb">Own curve of the user, one gain in dB per band.</param>
/// <param name="CrossfadeEnabled">Whether the players fade from one track into the next.</param>
/// <param name="CrossfadeSeconds">Seconds of the crossfade between tracks; 0 plays them gapless.</param>
/// <param name="CrossfadeAlbumsGapless">Whether consecutive tracks of the same album play without crossfade.</param>
/// <param name="SleepTimerFadeSeconds">Seconds the sleep timer fades out the music.</param>
/// <param name="SidebarCollapsed">Whether the sidebar of the desktop layout shows only its icons.</param>
/// <param name="VisualizationModule">The visualization of the Now Playing stage.</param>
/// <param name="VisualizationPreset">
/// Name of the chosen preset of the visualization; an empty or blank name forgets it, so the player picks one.
/// </param>
/// <param name="VisualizationAutoChange">Whether the visualization changes its preset automatically.</param>
/// <param name="VisualizationAutoChangeSeconds">
/// Seconds after which the visualization changes its preset, see <see cref="VisualizationSettingsLimits"/>.
/// </param>
/// <param name="VisualizationQuality">How much detail the visualization draws.</param>
/// <param name="ThemeId">Id of the theme of the web app, see <see cref="ThemeIds"/>.</param>
/// <param name="ThemeAppearance">Which variant of a theme with a light and a dark variant the user sees.</param>
/// <param name="AutoplayEnabled">Whether the players go on with matching songs when a list ends.</param>
/// <param name="AutoplayIncludeVideoOnly">Whether autoplay also chooses songs that only have a music video.</param>
public sealed record UpdateUserSettingsCommand(
    string? Language = null,
    ReplayGainMode? ReplayGain = null,
    PlayerViewMode? PlayerViewMode = null,
    bool? EqualizerEnabled = null,
    EqualizerPreset? EqualizerPreset = null,
    double? EqualizerPreampDb = null,
    IReadOnlyList<double>? EqualizerCustomGainsDb = null,
    bool? CrossfadeEnabled = null,
    int? CrossfadeSeconds = null,
    bool? CrossfadeAlbumsGapless = null,
    int? SleepTimerFadeSeconds = null,
    bool? SidebarCollapsed = null,
    VisualizationModule? VisualizationModule = null,
    string? VisualizationPreset = null,
    bool? VisualizationAutoChange = null,
    int? VisualizationAutoChangeSeconds = null,
    VisualizationQuality? VisualizationQuality = null,
    string? ThemeId = null,
    ThemeAppearance? ThemeAppearance = null,
    bool? AutoplayEnabled = null,
    bool? AutoplayIncludeVideoOnly = null);
