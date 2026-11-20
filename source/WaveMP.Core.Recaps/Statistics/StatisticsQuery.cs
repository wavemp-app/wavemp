namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// What statistics to compute.
/// </summary>
/// <param name="UserId">The user whose plays are counted.</param>
/// <param name="From">First day of the period; <c>null</c> from the first play on.</param>
/// <param name="To">Last day of the period; <c>null</c> up to today.</param>
/// <param name="TimeZone">Time zone of the days and hours.</param>
/// <param name="Top">Length of the rankings.</param>
/// <param name="ViewerId">The user who looks at the statistics: favorites and ratings of the tracks are theirs.</param>
public sealed record StatisticsQuery(
    Guid UserId,
    DateOnly? From,
    DateOnly? To,
    TimeZoneInfo TimeZone,
    int Top,
    Guid ViewerId);
