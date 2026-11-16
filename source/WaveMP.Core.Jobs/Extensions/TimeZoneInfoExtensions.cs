namespace WaveMP.Core.Jobs.Extensions;

public static class TimeZoneInfoExtensions
{
    /// <summary>
    /// The IANA id of the time zone, e.g. <c>Europe/Berlin</c> or <c>UTC</c>, also on Windows, whose system time zones
    /// have Windows ids; <c>null</c> if the time zone has none, e.g. a custom time zone or a local time zone whose
    /// name the system does not know.
    /// </summary>
    public static string? ToIanaId(this TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (timeZone.HasIanaId)
        {
            return timeZone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out string? ianaId) ? ianaId : null;
    }
}
