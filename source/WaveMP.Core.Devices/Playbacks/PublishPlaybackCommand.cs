namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// A device publishes the state of its player.
/// </summary>
/// <param name="Claim">Whether the device takes over playback because the user started it there; without it, only
/// the active device may publish.</param>
/// <param name="Playback">The state of the player.</param>
public sealed record PublishPlaybackCommand(bool Claim, LivePlaybackInput Playback);
