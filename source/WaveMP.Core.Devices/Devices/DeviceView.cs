using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// A device of a profile with its live state.
/// </summary>
/// <param name="Id">Id the device chose for itself.</param>
/// <param name="Name">The name to show: the one the user gave it, otherwise the one it reports.</param>
/// <param name="ReportedName">The name the device reports for itself.</param>
/// <param name="CustomName">The name the user gave it; <c>null</c> if none.</param>
/// <param name="Type">Kind of the device.</param>
/// <param name="Online">Whether the device is open and connected right now.</param>
/// <param name="RemoteControl">Whether the device allows remote control right now; never while it is closed.</param>
/// <param name="Active">Whether the device is the active playback device of the profile (also while it is closed:
/// it continues when it comes back, unless another device takes over).</param>
/// <param name="LastActiveAt">When the device was last active.</param>
public sealed record DeviceView(
    string Id,
    string Name,
    string ReportedName,
    string? CustomName,
    DeviceType Type,
    bool Online,
    bool RemoteControl,
    bool Active,
    DateTimeOffset LastActiveAt);
