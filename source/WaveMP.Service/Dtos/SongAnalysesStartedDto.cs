namespace WaveMP.Service.Dtos;

/// <summary>
/// A new analysis of the whole library was requested.
/// </summary>
public sealed class SongAnalysesStartedDto
{
    /// <summary>
    /// Number of file analyses that are renewed; files never analyzed are analyzed anyway.
    /// </summary>
    public required int Renewed { get; init; }
}
