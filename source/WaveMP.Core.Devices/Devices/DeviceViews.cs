using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// Combines the stored devices of a profile with their live state.
/// </summary>
internal static class DeviceViews
{
    /// <summary>
    /// The devices in the order the clients list them: open devices first, then the most recently active, then by id.
    /// </summary>
    public static IReadOnlyList<DeviceView> Build(IEnumerable<Device> devices, LiveDevices live)
    {
        return [.. devices
            .Select(device => ToView(device, live))
            .OrderByDescending(device => device.Online)
            .ThenByDescending(device => device.LastActiveAt)
            .ThenBy(device => device.Id, StringComparer.Ordinal)];
    }

    private static DeviceView ToView(Device device, LiveDevices live)
    {
        bool online = live.RemoteControlByDeviceId.TryGetValue(device.DeviceId, out bool remoteControl);
        return new DeviceView(
            device.DeviceId,
            device.CustomName ?? device.ReportedName,
            device.ReportedName,
            device.CustomName,
            device.Type,
            online,
            online && remoteControl,
            device.DeviceId == live.ActiveDeviceId,
            device.LastActiveAt);
    }
}
