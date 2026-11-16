namespace WaveMP.Data.Entities;

/// <summary>
/// How much detail the visualizations of the players draw. Stored as its name.
/// </summary>
public enum VisualizationQuality
{
    /// <summary>
    /// The player chooses the level by the frame rate of the device; the default.
    /// </summary>
    Auto,

    /// <summary>
    /// Least detail, for weak devices.
    /// </summary>
    Low,

    /// <summary>
    /// Medium detail.
    /// </summary>
    Medium,

    /// <summary>
    /// Most detail.
    /// </summary>
    High,
}
