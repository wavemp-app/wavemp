using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// What a device gets when it registers: its devices and the live playback of its profile.
/// </summary>
/// <param name="DeviceId">The id the device registered with.</param>
/// <param name="Devices">All devices of the profile, open ones first.</param>
/// <param name="Playback">The live playback with its version.</param>
public sealed record DeviceSessionSnapshot(
    string DeviceId,
    IReadOnlyList<DeviceView> Devices,
    LivePlaybackView Playback);
