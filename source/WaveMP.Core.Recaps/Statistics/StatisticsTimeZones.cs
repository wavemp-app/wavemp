namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// The time zone the days of a period and the hours of the plays are counted in.
/// </summary>
public static class StatisticsTimeZones
{
    /// <summary>
    /// Time zone without a choice of the client.
    /// </summary>
    public const string Default = "UTC";

    /// <summary>
    /// Longest id of a time zone.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Finds a time zone by its IANA id, e.g. <c>Europe/Berlin</c>.
    /// </summary>
    public static bool TryFind(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxLength)
        {
            return false;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? found))
        {
            return false;
        }

        timeZone = found;
        return true;
    }

    /// <summary>
    /// The first moment of a day in the time zone, in UTC. A day that starts in a gap of a change to daylight saving
    /// time
    /// starts at the end of the gap.
    /// </summary>
    public static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        for (int minutes = 0; minutes <= 24 * 60; minutes += 15)
        {
            DateTime candidate = local.AddMinutes(minutes);
            if (!timeZone.IsInvalidTime(candidate))
            {
                return new DateTimeOffset(candidate, timeZone.GetUtcOffset(candidate)).ToUniversalTime();
            }
        }

        return new DateTimeOffset(local, timeZone.BaseUtcOffset).ToUniversalTime();
    }

    /// <summary>
    /// The day of a moment in the time zone.
    /// </summary>
    public static DateOnly DayOf(DateTimeOffset moment, TimeZoneInfo timeZone)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, timeZone).DateTime);
    }
}
