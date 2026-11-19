namespace WaveMP.Service.Dtos;

/// <summary>
/// The songs autoplay plays next.
/// </summary>
public sealed class AutoplayResultDto
{
    /// <summary>
    /// The songs in the order they play, at most as many as requested; empty if no matching song is left. Songs the
    /// client cannot play (e.g. a file that is gone since the last scan) are left out.
    /// </summary>
    public required IReadOnlyList<AutoplayTrackDto> Items { get; init; }
}
