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
    /// A-Force, the own visualization of light in motion; its presets are its modes, its colors are color schemes.
    /// </summary>
    AForce,

    /// <summary>
    /// BlackCap, the own visualization with organic, floating 3D shapes and light effects; its presets are its modes,
    /// its colors are color schemes.
    /// </summary>
    BlackCap,
}
