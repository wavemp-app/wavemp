namespace WaveMP.Data.Entities;

/// <summary>
/// Repeat mode of a player.
/// </summary>
public enum RepeatMode
{
    /// <summary>
    /// The queue plays once.
    /// </summary>
    Off,

    /// <summary>
    /// The queue starts again after its last entry.
    /// </summary>
    All,

    /// <summary>
    /// The current track plays again and again.
    /// </summary>
    One,
}
