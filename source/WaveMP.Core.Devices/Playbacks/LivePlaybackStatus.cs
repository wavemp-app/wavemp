namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// What the player of the active device does.
/// </summary>
public enum LivePlaybackStatus
{
    /// <summary>
    /// Nothing is loaded.
    /// </summary>
    Idle,

    /// <summary>
    /// A song is loading.
    /// </summary>
    Loading,

    /// <summary>
    /// A song plays.
    /// </summary>
    Playing,

    /// <summary>
    /// A song is paused.
    /// </summary>
    Paused,
}
