using WaveMP.Data.Projections;

namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// The statistics of the plays of a user in a period, counted in a time zone.
/// </summary>
/// <param name="From">First day of the period; for all plays the day of the first play.</param>
/// <param name="To">Last day of the period.</param>
/// <param name="TimeZone">IANA time zone of the days and hours.</param>
/// <param name="Totals">Numbers of the whole period.</param>
/// <param name="TopTracks">The most played tracks.</param>
/// <param name="TopArtists">The most played artists (artists of the tracks).</param>
/// <param name="TopAlbums">The most played albums.</param>
/// <param name="TopGenres">The most played genres.</param>
/// <param name="NewArtists">Artists played for the first time in the period, the most played first.</param>
/// <param name="Timeline">The plays over the period.</param>
/// <param name="Hours">The plays per hour of the day, 0 to 23.</param>
public sealed record ListeningStatistics(
    DateOnly From,
    DateOnly To,
    string TimeZone,
    StatisticsTotals Totals,
    IReadOnlyList<TopTrack> TopTracks,
    IReadOnlyList<TopArtist> TopArtists,
    IReadOnlyList<TopAlbum> TopAlbums,
    IReadOnlyList<TopGenre> TopGenres,
    IReadOnlyList<NewArtist> NewArtists,
    Timeline Timeline,
    IReadOnlyList<HourStatistics> Hours);

/// <summary>
/// Numbers of a whole period.
/// </summary>
/// <param name="Plays">Number of plays.</param>
/// <param name="ListeningTime">Sum of the durations of the played tracks.</param>
/// <param name="Tracks">Number of different tracks.</param>
/// <param name="Artists">Number of different artists.</param>
/// <param name="Albums">Number of different albums.</param>
/// <param name="Genres">Number of different genres.</param>
/// <param name="NewArtists">Number of artists played for the first time.</param>
/// <param name="ActiveDays">Number of days with at least one play.</param>
public sealed record StatisticsTotals(
    int Plays,
    TimeSpan ListeningTime,
    int Tracks,
    int Artists,
    int Albums,
    int Genres,
    int NewArtists,
    int ActiveDays);

/// <summary>
/// A track among the most played.
/// </summary>
public sealed record TopTrack(TrackRow Track, int Plays, TimeSpan ListeningTime);

/// <summary>
/// An artist among the most played; <paramref name="CoverTrackId"/> is its most played track with a cover.
/// </summary>
public sealed record TopArtist(
    Guid Id,
    string Name,
    int Plays,
    TimeSpan ListeningTime,
    int Tracks,
    Guid? CoverTrackId);

/// <summary>
/// An album among the most played; <paramref name="CoverTrackId"/> is its most played track with a cover.
/// <paramref name="IsCompilation"/> tells whether the album is a compilation now, as its played tracks name it.
/// </summary>
public sealed record TopAlbum(
    Guid Id,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    bool IsCompilation,
    int Plays,
    TimeSpan ListeningTime,
    int Tracks,
    Guid? CoverTrackId);

/// <summary>
/// A genre among the most played, named in its most played spelling.
/// </summary>
public sealed record TopGenre(string Name, int Plays, TimeSpan ListeningTime, int Tracks, Guid? CoverTrackId);

/// <summary>
/// An artist played for the first time in the period.
/// </summary>
public sealed record NewArtist(
    Guid Id,
    string Name,
    DateTimeOffset FirstPlayedAt,
    int Plays,
    TimeSpan ListeningTime,
    Guid? CoverTrackId);

/// <summary>
/// The plays over the period in steps of the granularity, without gaps.
/// </summary>
public sealed record Timeline(TimelineGranularity Granularity, IReadOnlyList<TimelineBucket> Buckets);

/// <summary>
/// One step of the timeline, starting on <paramref name="Start"/> (the first day of the day, week, month or year,
/// which may lie before the period).
/// </summary>
public sealed record TimelineBucket(DateOnly Start, int Plays, TimeSpan ListeningTime);

/// <summary>
/// The plays that started in an hour of the day.
/// </summary>
public sealed record HourStatistics(int Hour, int Plays, TimeSpan ListeningTime);
