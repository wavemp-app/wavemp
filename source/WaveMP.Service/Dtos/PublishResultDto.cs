namespace WaveMP.Service.Dtos;

/// <summary>
/// Result of publishing the state of a player.
/// </summary>
public sealed class PublishResultDto
{
    public required PublishStatusDto Status { get; init; }

    /// <summary>
    /// The version of the live state afterwards (unchanged unless accepted); 0 for <c>NotRegistered</c>.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// The version at which the live queue changed last; 0 for <c>NotRegistered</c>.
    /// </summary>
    public required long QueueVersion { get; init; }

    /// <summary>
    /// The rejected fields of an invalid state; empty otherwise.
    /// </summary>
    public required IReadOnlyList<ValidationErrorDto> Errors { get; init; }
}
