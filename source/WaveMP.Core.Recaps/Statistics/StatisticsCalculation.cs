namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// The statistics as <see cref="ListeningStatisticsCalculator"/> counts them from the plays: tracks are named by their
/// ids, and every group lists its most played tracks, so the cover of the first one with a cover can be chosen.
/// </summary>
internal sealed record StatisticsCalculation(
    StatisticsTotals Totals,
    IReadOnlyList<CountedTrack> TopTracks,
    IReadOnlyList<CountedGroup> TopArtists,
    IReadOnlyList<CountedGroup> TopAlbums,
    IReadOnlyList<CountedGroup> TopGenres,
    IReadOnlyList<CountedGroup> NewArtists,
    Timeline Timeline,
    IReadOnlyList<HourStatistics> Hours);

/// <summary>
/// A track with its plays in the period.
/// </summary>
internal sealed record CountedTrack(Guid TrackId, int Plays, TimeSpan ListeningTime);

/// <summary>
/// An artist, album or genre with its plays in the period. <paramref name="Id"/> is empty for genres;
/// <paramref name="ArtistId"/> and <paramref name="ArtistName"/> name the artist of an album;
/// <paramref name="FirstPlayedAt"/> is the first play of a new artist; <paramref name="TrackIds"/> are its most played
/// tracks, the most played first.
/// </summary>
internal sealed record CountedGroup(
    Guid Id,
    string Name,
    int Plays,
    TimeSpan ListeningTime,
    int Tracks,
    IReadOnlyList<Guid> TrackIds,
    Guid? ArtistId = null,
    string? ArtistName = null,
    DateTimeOffset? FirstPlayedAt = null);
