namespace WaveMP.Data.Entities;

/// <summary>
/// How strongly the visualizations of the players move, react and change their brightness. Stored as its name.
/// </summary>
public enum VisualizationIntensity
{
    /// <summary>
    /// The full motion and reaction of the visualizations; the default.
    /// </summary>
    Normal,

    /// <summary>
    /// Calmer motion, weaker reactions, slower Milkdrop presets and slower changes of brightness, for people who are
    /// sensitive to motion or flashing light.
    /// </summary>
    Reduced,
}
