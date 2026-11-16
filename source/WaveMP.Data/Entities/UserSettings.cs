namespace WaveMP.Data.Entities;

/// <summary>
/// Settings of a user, one row per user. Later settings are added as further columns.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Number of bands of the equalizer, and so of the values of <see cref="EqualizerCustomGainsDb"/>.
    /// </summary>
    public const int EqualizerBandCount = 10;

    /// <summary>
    /// Longest name of a preset in <see cref="VisualizationPreset"/>.
    /// </summary>
    public const int VisualizationPresetMaxLength = 200;

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public Language Language { get; set; } = Language.En;

    /// <summary>
    /// Volume normalization of the player; off by default.
    /// </summary>
    public ReplayGainMode ReplayGain { get; set; } = ReplayGainMode.Off;

    /// <summary>
    /// What the Now Playing stage shows; the video by default.
    /// </summary>
    public PlayerViewMode PlayerViewMode { get; set; } = PlayerViewMode.Video;

    /// <summary>
    /// Whether the sidebar of the desktop layout shows only its icons; expanded by default.
    /// </summary>
    public bool SidebarCollapsed { get; set; }

    /// <summary>
    /// Whether the players shape the sound with the equalizer; off by default.
    /// </summary>
    public bool EqualizerEnabled { get; set; }

    /// <summary>
    /// Curve of the equalizer; <see cref="EqualizerPreset.Flat"/> by default.
    /// </summary>
    public EqualizerPreset EqualizerPreset { get; set; } = EqualizerPreset.Flat;

    /// <summary>
    /// Gain in dB before the bands of the equalizer.
    /// </summary>
    public double EqualizerPreampDb { get; set; }

    /// <summary>
    /// Own curve of the user in dB, one value per band from the lowest to the highest frequency. It stays when the
    /// user picks a preset and applies with <see cref="EqualizerPreset.Custom"/>.
    /// </summary>
    public double[] EqualizerCustomGainsDb { get; set; } = new double[EqualizerBandCount];

    /// <summary>
    /// Whether the players fade from one track into the next; off by default. Turning it off keeps
    /// <see cref="CrossfadeSeconds"/>, so turning it on again fades as long as before.
    /// </summary>
    public bool CrossfadeEnabled { get; set; }

    /// <summary>
    /// Seconds the players fade from one track into the next while <see cref="CrossfadeEnabled"/> is on; 0 plays them
    /// gapless. 5 by default.
    /// </summary>
    public int CrossfadeSeconds { get; set; } = 5;

    /// <summary>
    /// Whether consecutive tracks of the same album play gapless even with a crossfade; on by default.
    /// </summary>
    public bool CrossfadeAlbumsGapless { get; set; } = true;

    /// <summary>
    /// Seconds the sleep timer fades out the music before it stops; 10 by default.
    /// </summary>
    public int SleepTimerFadeSeconds { get; set; } = 10;

    /// <summary>
    /// The visualization the Now Playing stage shows; <see cref="VisualizationModule.Milkdrop"/> by default.
    /// </summary>
    public VisualizationModule VisualizationModule { get; set; } = VisualizationModule.Milkdrop;

    /// <summary>
    /// Name of the preset of <see cref="VisualizationModule"/> the user chose last; <c>null</c> lets the player pick
    /// one. The players know the presets of their modules, the server only keeps the name.
    /// </summary>
    public string? VisualizationPreset { get; set; }

    /// <summary>
    /// Whether the visualization changes its preset automatically; on by default. Turning it off keeps
    /// <see cref="VisualizationAutoChangeSeconds"/>, so turning it on again changes as often as before.
    /// </summary>
    public bool VisualizationAutoChange { get; set; } = true;

    /// <summary>
    /// Seconds after which the visualization changes its preset while <see cref="VisualizationAutoChange"/> is on;
    /// 30 by default.
    /// </summary>
    public int VisualizationAutoChangeSeconds { get; set; } = 30;
}
