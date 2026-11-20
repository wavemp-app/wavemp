namespace WaveMP.Service.Dtos;

/// <summary>
/// The statistics of the plays of a profile in a period: rankings, listening time and the plays over time. Every play
/// counts with the whole duration of its track. Rankings put the most plays first, then the longest listening time.
/// </summary>
public sealed class ListeningStatisticsDto
{
    /// <summary>
    /// First day of the period; for all plays the day of the first play.
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// Last day of the period.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// IANA time zone the days and hours are counted in.
    /// </summary>
    public required string TimeZone { get; init; }

    public required StatisticsTotalsDto Totals { get; init; }

    /// <summary>
    /// The most played tracks, at most as many as requested with top.
    /// </summary>
    public required IReadOnlyList<TopTrackDto> TopTracks { get; init; }

    /// <summary>
    /// The most played artists (artists of the tracks); tracks without artist do not count.
    /// </summary>
    public required IReadOnlyList<TopArtistDto> TopArtists { get; init; }

    /// <summary>
    /// The most played albums; tracks without album do not count.
    /// </summary>
    public required IReadOnlyList<TopAlbumDto> TopAlbums { get; init; }

    /// <summary>
    /// The most played genres, compared without regard to case; tracks without genre do not count.
    /// </summary>
    public required IReadOnlyList<TopGenreDto> TopGenres { get; init; }

    /// <summary>
    /// Artists the profile played for the first time in the period, the most played first.
    /// </summary>
    public required IReadOnlyList<NewArtistDto> NewArtists { get; init; }

    public required TimelineDto Timeline { get; init; }

    /// <summary>
    /// The plays per hour of the day in the time zone: 24 entries, from 0 to 23.
    /// </summary>
    public required IReadOnlyList<HourStatisticsDto> Hours { get; init; }
}
