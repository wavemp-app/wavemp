namespace WaveMP.Service.Queries;

/// <summary>
/// Limits of the statistics and recaps: the days of a period and the length of the rankings (query parameter
/// <c>top</c>).
/// </summary>
public static class StatisticsLimits
{
    public const int DefaultTop = 10;

    public const int MaxTop = 50;

    /// <summary>
    /// Earliest first day of a period.
    /// </summary>
    public static readonly DateOnly MinDate = new(1900, 1, 1);

    /// <summary>
    /// Latest last day of a period.
    /// </summary>
    public static readonly DateOnly MaxDate = new(2999, 12, 31);

    /// <summary>
    /// Whether the day can be part of a period.
    /// </summary>
    public static bool IsInRange(DateOnly day)
    {
        return day >= MinDate && day <= MaxDate;
    }
}
