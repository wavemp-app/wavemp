namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the current profile.
/// </summary>
public sealed class UserSettingsDto
{
    /// <summary>
    /// Language of the user interface as BCP 47 language tag, e.g. "en", "de" or "pt-BR"; "en" by default. A client
    /// without translations for the language shows English, or the language without its region if it has that (e.g.
    /// "de" for "de-AT").
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Whether the players even out the volume with the ReplayGain tags; Off by default.
    /// </summary>
    public required ReplayGainModeDto ReplayGain { get; init; }

    /// <summary>
    /// What the Now Playing view shows; Video by default. Clients keep the choice of the profile with it.
    /// </summary>
    public required PlayerViewModeDto PlayerViewMode { get; init; }

    /// <summary>
    /// Whether the sidebar of the desktop layout shows only its icons; false (expanded) by default. Clients without
    /// a sidebar ignore it.
    /// </summary>
    public required bool SidebarCollapsed { get; init; }

    /// <summary>
    /// Theme of the web app. Clients without themes ignore it.
    /// </summary>
    public required ThemeSettingsDto Theme { get; init; }

    /// <summary>
    /// Equalizer of the players.
    /// </summary>
    public required EqualizerSettingsDto Equalizer { get; init; }

    /// <summary>
    /// Crossfade between tracks.
    /// </summary>
    public required CrossfadeSettingsDto Crossfade { get; init; }

    /// <summary>
    /// Sleep timer of the players.
    /// </summary>
    public required SleepTimerSettingsDto SleepTimer { get; init; }

    /// <summary>
    /// Autoplay at the end of a list.
    /// </summary>
    public required AutoplaySettingsDto Autoplay { get; init; }

    /// <summary>
    /// Videos of the Now Playing view.
    /// </summary>
    public required VideoSettingsDto Video { get; init; }

    /// <summary>
    /// Visualization of the Now Playing view.
    /// </summary>
    public required VisualizationSettingsDto Visualization { get; init; }
}
