namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// Length of the steps of the distribution over time; it follows the length of the period.
/// </summary>
public enum TimelineGranularity
{
    Day,

    /// <summary>
    /// Weeks from Monday to Sunday.
    /// </summary>
    Week,

    Month,

    Year,
}
