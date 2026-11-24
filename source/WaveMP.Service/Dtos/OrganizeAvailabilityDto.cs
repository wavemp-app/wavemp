namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether files can be organized: per media source, and the operation that is queued or running.
/// </summary>
public sealed class OrganizeAvailabilityDto
{
    public required IReadOnlyList<SourceAvailabilityDto> Sources { get; init; }

    /// <summary>
    /// Whether at least one source can be organized.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// The operation that is queued or running; previews and further runs wait for it.
    /// </summary>
    public Guid? ActiveOperationId { get; init; }
}

/// <summary>
/// Whether the files of a media source can be organized.
/// </summary>
public sealed class SourceAvailabilityDto
{
    public required Guid SourceId { get; init; }

    public required string Name { get; init; }

    public required SourceAvailabilityStatusDto Status { get; init; }
}
