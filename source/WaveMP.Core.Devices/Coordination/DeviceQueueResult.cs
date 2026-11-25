using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// Outcome of reading the live queue.
/// </summary>
/// <param name="Status"><see cref="CommandStatus.Accepted"/> or <see cref="CommandStatus.NotRegistered"/>.</param>
/// <param name="Queue">The live queue; <c>null</c> if the connection has not registered a device.</param>
public sealed record DeviceQueueResult(CommandStatus Status, DeviceQueueView? Queue);
