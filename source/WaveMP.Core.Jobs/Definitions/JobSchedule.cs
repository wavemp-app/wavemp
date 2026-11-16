namespace WaveMP.Core.Jobs.Definitions;

/// <summary>
/// The periodic schedule of a job: every <see cref="Interval"/>, or every day at a time of day
/// (<see cref="Daily"/>).
/// </summary>
/// <param name="Interval">Time between two runs; one day for daily schedules.</param>
/// <param name="FirstRunDelay">Time until the first run after the schedule was created; not used by daily
/// schedules, whose first run is the next time the clock shows <see cref="DailyAt"/>.</param>
public sealed record JobSchedule(TimeSpan Interval, TimeSpan FirstRunDelay)
{
    /// <summary>
    /// Time of day of a daily schedule in <see cref="TimeZone"/>, or <c>null</c> for a schedule by interval.
    /// </summary>
    public TimeOnly? DailyAt { get; init; }

    /// <summary>
    /// Time zone of <see cref="DailyAt"/>; the local time zone of the server if <c>null</c>.
    /// </summary>
    public TimeZoneInfo? TimeZone { get; init; }

    /// <summary>
    /// Runs the job every day at <paramref name="time"/> (hours and minutes) in <paramref name="timeZone"/>; also on
    /// days with a change of the daylight saving time.
    /// </summary>
    public static JobSchedule Daily(TimeOnly time, TimeZoneInfo timeZone)
    {
        return new JobSchedule(TimeSpan.FromDays(1), TimeSpan.Zero)
        {
            DailyAt = new TimeOnly(time.Hour, time.Minute),
            TimeZone = timeZone,
        };
    }
}
