namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// The live part of the devices of a profile.
/// </summary>
/// <param name="RemoteControlByDeviceId">The open devices by their id, with whether each allows remote control.
/// </param>
/// <param name="ActiveDeviceId">The active playback device; <c>null</c> if none was chosen yet.</param>
internal sealed record LiveDevices(IReadOnlyDictionary<string, bool> RemoteControlByDeviceId, string? ActiveDeviceId)
{
    public static LiveDevices None { get; } = new(new Dictionary<string, bool>(), null);
}
