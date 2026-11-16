namespace WaveMP.Service.Dtos;

/// <summary>
/// A background job with its schedule, its next and its last run.
/// </summary>
public sealed class JobDto
{
    public required JobIdDto Id { get; init; }

    public required JobStateDto State { get; init; }

    /// <summary>
    /// Minutes between two scheduled runs; null for jobs that only run when started.
    /// </summary>
    public int? IntervalMinutes { get; init; }

    /// <summary>
    /// Time of day as <c>HH:mm</c> for jobs that run every day at a fixed time, in the time zone of the server
    /// (<c>timeZone</c>); <see cref="IntervalMinutes"/> is then 1440.
    /// </summary>
    public string? DailyAt { get; init; }

    /// <summary>
    /// IANA id of the time zone of <c>dailyAt</c>, e.g. <c>UTC</c> or <c>Europe/Berlin</c>: the time zone of
    /// the server (<c>TZ</c>). Null for jobs without a daily schedule and when the server does not know the IANA id
    /// of its time zone.
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// When the schedule runs the job next.
    /// </summary>
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>
    /// The most recent run, also while it is queued or running.
    /// </summary>
    public JobRunDto? LastRun { get; init; }
}
