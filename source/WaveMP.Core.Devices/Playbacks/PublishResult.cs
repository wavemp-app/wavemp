using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// Outcome of publishing the state of a player.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Version">The version of the live state after the publication (unchanged unless accepted); 0 for
/// <see cref="PublishStatus.NotRegistered"/>.</param>
/// <param name="QueueVersion">The version at which the live queue changed last.</param>
/// <param name="Errors">The rejected fields of an invalid state; empty otherwise.</param>
public sealed record PublishResult(
    PublishStatus Status,
    long Version,
    long QueueVersion,
    IReadOnlyList<DeviceValidationError> Errors)
{
    internal static PublishResult NotRegistered { get; } = new(PublishStatus.NotRegistered, 0, 0, []);
}
