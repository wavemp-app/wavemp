namespace WaveMP.Service.Dtos;

/// <summary>
/// Result of a command, a hand-over or a change of the permission.
/// </summary>
public sealed class CommandResultDto
{
    public required CommandStatusDto Status { get; init; }

    /// <summary>
    /// The version of the live state afterwards; 0 for <c>NotRegistered</c> and for commands rejected before the
    /// server looked at the devices.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// The version at which the live queue changed last; 0 where <c>version</c> is 0.
    /// </summary>
    public required long QueueVersion { get; init; }

    /// <summary>
    /// The rejected fields of an invalid command; empty otherwise.
    /// </summary>
    public required IReadOnlyList<ValidationErrorDto> Errors { get; init; }
}
