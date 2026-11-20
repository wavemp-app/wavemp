namespace WaveMP.Service.Dtos;

/// <summary>
/// Numbers of a whole period of the statistics.
/// </summary>
public sealed class StatisticsTotalsDto
{
    /// <summary>
    /// Number of plays.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Sum of the durations of the played tracks in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }

    /// <summary>
    /// Number of different tracks.
    /// </summary>
    public required int Tracks { get; init; }

    /// <summary>
    /// Number of different artists.
    /// </summary>
    public required int Artists { get; init; }

    /// <summary>
    /// Number of different albums.
    /// </summary>
    public required int Albums { get; init; }

    /// <summary>
    /// Number of different genres.
    /// </summary>
    public required int Genres { get; init; }

    /// <summary>
    /// Number of artists played for the first time in the period.
    /// </summary>
    public required int NewArtists { get; init; }

    /// <summary>
    /// Number of days with at least one play.
    /// </summary>
    public required int ActiveDays { get; init; }
}
