namespace WaveMP.Service.Dtos;

/// <summary>
/// A shared recap with the statistics of the profile that shared it.
/// </summary>
public sealed class SharedRecapDto
{
    public required RecapShareDto Share { get; init; }

    public required ListeningStatisticsDto Statistics { get; init; }
}
