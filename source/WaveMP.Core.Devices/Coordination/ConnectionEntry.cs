namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// A connection of a profile that registered a device.
/// </summary>
/// <param name="SessionId">The session the connection was opened with.</param>
/// <param name="DeviceId">The device it registered; <c>null</c> after a newer connection replaced it.</param>
internal sealed record ConnectionEntry(Guid SessionId, string? DeviceId);
