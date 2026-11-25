using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// Outcome of a command, a hand-over or a change of the permission.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Version">The version of the live state afterwards; 0 for <see cref="CommandStatus.NotRegistered"/>.
/// </param>
/// <param name="QueueVersion">The version at which the live queue changed last.</param>
/// <param name="Errors">The rejected fields of an invalid command; empty otherwise.</param>
public sealed record CommandResult(
    CommandStatus Status,
    long Version,
    long QueueVersion,
    IReadOnlyList<DeviceValidationError> Errors)
{
    internal static CommandResult NotRegistered { get; } = new(CommandStatus.NotRegistered, 0, 0, []);

    internal static CommandResult Invalid(IReadOnlyList<DeviceValidationError> errors)
    {
        return new CommandResult(CommandStatus.Invalid, 0, 0, errors);
    }
}
