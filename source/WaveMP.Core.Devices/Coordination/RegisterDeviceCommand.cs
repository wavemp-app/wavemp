using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// A connection registers the device it runs on.
/// </summary>
/// <param name="DeviceId">Id the device chose for itself and keeps, see <see cref="Validations.DeviceRules"/>.</param>
/// <param name="Name">Name the device reports for itself, 1 to 100 characters.</param>
/// <param name="Type">Kind of the device.</param>
/// <param name="RemoteControl">Whether the user allowed remote control on the running page; a reconnect of the same
/// page keeps it, a reload starts without it.</param>
public sealed record RegisterDeviceCommand(string DeviceId, string Name, DeviceType Type, bool RemoteControl);
