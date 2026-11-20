namespace WaveMP.Service.Dtos;

/// <summary>
/// One step of the timeline of the statistics.
/// </summary>
public sealed class TimelineBucketDto
{
    /// <summary>
    /// First day of the step: the day, the Monday of the week, the first of the month or of the year; may lie before
    /// the period.
    /// </summary>
    public required DateOnly Start { get; init; }

    /// <summary>
    /// Number of plays of the period in the step.
    /// </summary>
    public required int Plays { get; init; }

    /// <summary>
    /// Listening time of the period in the step in milliseconds.
    /// </summary>
    public required long ListeningMilliseconds { get; init; }
}
