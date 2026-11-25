namespace WaveMP.Service.Dtos;

/// <summary>
/// Result of reading the live queue.
/// </summary>
public sealed class DeviceQueueResultDto
{
    /// <summary>
    /// <c>Accepted</c> or <c>NotRegistered</c>.
    /// </summary>
    public required CommandStatusDto Status { get; init; }

    /// <summary>
    /// The live queue; null for <c>NotRegistered</c>.
    /// </summary>
    public required DeviceQueueDto? Queue { get; init; }
}
