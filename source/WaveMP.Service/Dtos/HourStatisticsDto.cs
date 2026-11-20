namespace WaveMP.Service.Dtos;

/// <summary>
/// The plays that started in an hour of the day.
/// </summary>
public sealed class HourStatisticsDto
{
    /// <summary>
    /// Hour of the day, 0 to 23.
    /// </summary>
    public required int Hour { get; init; }

    /// <summary>
    /// Number of plays.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Listening time in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }
}
