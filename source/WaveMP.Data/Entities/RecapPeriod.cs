namespace WaveMP.Data.Entities;

/// <summary>
/// The period a recap was chosen with. The dates of a shared recap are fixed when it is shared; the period only names
/// how they were chosen.
/// </summary>
public enum RecapPeriod
{
    /// <summary>
    /// The last month up to the day of sharing.
    /// </summary>
    LastMonth,

    /// <summary>
    /// The last three months up to the day of sharing.
    /// </summary>
    LastThreeMonths,

    /// <summary>
    /// The last twelve months up to the day of sharing.
    /// </summary>
    LastYear,

    /// <summary>
    /// The calendar year of the day of sharing, up to that day.
    /// </summary>
    CurrentYear,

    /// <summary>
    /// The whole play history up to the day of sharing.
    /// </summary>
    AllTime,

    /// <summary>
    /// Dates the user chose.
    /// </summary>
    Custom,
}
