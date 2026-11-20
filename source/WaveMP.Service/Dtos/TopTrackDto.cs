namespace WaveMP.Service.Dtos;

/// <summary>
/// A track among the most played of a period.
/// </summary>
public sealed class TopTrackDto
{
    public required TrackDto Track { get; init; }

    /// <summary>
    /// Number of plays in the period.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Listening time in the period in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }
}
