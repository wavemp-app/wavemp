namespace WaveMP.Data.Entities;

/// <summary>
/// A visualization of the players: an engine that draws the music, with its own presets. Stored as its name.
/// </summary>
public enum VisualizationModule
{
    /// <summary>
    /// Milkdrop presets; the default.
    /// </summary>
    Milkdrop,

    /// <summary>
    /// A-Force, the own visualization with flowing waveforms and particles; its presets are style variants.
    /// </summary>
    AForce,
}
