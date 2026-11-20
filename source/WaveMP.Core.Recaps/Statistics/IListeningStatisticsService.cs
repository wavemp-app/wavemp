namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// The statistics of the play history of a user: top tracks, artists, albums and genres, listening time, the plays
/// over time and the artists played for the first time.
/// </summary>
public interface IListeningStatisticsService
{
    /// <summary>
    /// Counts the plays of the period. Without a first day the period starts with the first play of the user (today
    /// without plays); a first day after the last day is moved to the last day.
    /// </summary>
    Task<ListeningStatistics> GetAsync(StatisticsQuery query, CancellationToken cancellationToken = default);
}
