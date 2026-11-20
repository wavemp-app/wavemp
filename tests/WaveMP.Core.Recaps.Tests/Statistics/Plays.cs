using WaveMP.Data.Projections;

namespace WaveMP.Core.Recaps.Tests.Statistics;

/// <summary>
/// Builds plays and tracks of the statistics for the tests.
/// </summary>
internal static class Plays
{
    public static readonly Guid Ballad = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid Opening = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    public static readonly Guid Closing = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    public static readonly Guid Single = Guid.Parse("00000000-0000-0000-0000-00000000000d");

    public static readonly Guid Aurora = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Breeze = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    public static readonly Guid Daybreak = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    public static readonly Guid Evening = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    /// <summary>
    /// A play of one of the tracks: Ballad (Aurora, Daybreak, Pop, 4 minutes), Opening (Aurora, Daybreak, pop, 3
    /// minutes), Closing (Breeze, Evening, Rock, 5 minutes) and Single (no artist, album or genre, 2 minutes).
    /// </summary>
    public static PlayStatisticsRow Of(Guid track, DateTimeOffset playedAt)
    {
        if (track == Ballad)
        {
            return new PlayStatisticsRow(
                playedAt, Ballad, "Ballad", Aurora, "Aurora", Daybreak, "Daybreak", Aurora, "Aurora", "Pop",
                TimeSpan.FromMinutes(4));
        }

        if (track == Opening)
        {
            return new PlayStatisticsRow(
                playedAt, Opening, "Opening", Aurora, "Aurora", Daybreak, "Daybreak", Aurora, "Aurora", "pop",
                TimeSpan.FromMinutes(3));
        }

        if (track == Closing)
        {
            return new PlayStatisticsRow(
                playedAt, Closing, "Closing", Breeze, "Breeze", Evening, "Evening", Breeze, "Breeze", "Rock",
                TimeSpan.FromMinutes(5));
        }

        return new PlayStatisticsRow(
            playedAt, Single, "Single", null, null, null, null, null, null, null, TimeSpan.FromMinutes(2));
    }

    public static TrackRow Row(Guid id, bool hasCover)
    {
        return new TrackRow(
            id,
            "Track " + id.ToString("N")[^2..],
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            TimeSpan.FromMinutes(3),
            true,
            false,
            hasCover,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
