namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// Moves playback to another device (or to the calling device itself: "play here"), at the same position.
/// </summary>
/// <param name="TargetDeviceId">The device that plays afterwards.</param>
/// <param name="Play">Whether it starts playing at once; otherwise it loads the state paused.</param>
/// <param name="ExpectedVersion">The version of the live state the device saw; a state that changed since then
/// answers <see cref="CommandStatus.Conflict"/>. <c>null</c> skips the check.</param>
public sealed record TransferPlaybackCommand(string TargetDeviceId, bool Play, long? ExpectedVersion = null);
