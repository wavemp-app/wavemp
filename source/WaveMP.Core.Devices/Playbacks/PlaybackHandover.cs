namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// Playback moved to another device.
/// </summary>
/// <param name="FromDeviceId">The device that was active before; <c>null</c> if none was.</param>
/// <param name="Play">Whether the new active device starts playing at once (otherwise it loads the state paused).
/// </param>
public sealed record PlaybackHandover(string? FromDeviceId, bool Play);
