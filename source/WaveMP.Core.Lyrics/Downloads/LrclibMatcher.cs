namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Chooses the lyrics of LRCLIB that fit a track best: only tracks whose duration differs by at most
/// <see cref="DurationTolerance"/> (if both durations are known), synchronized lyrics before plain ones, then tracks of
/// the same album, then the closest duration.
/// </summary>
public static class LrclibMatcher
{
    /// <summary>
    /// Largest difference of the durations; LRCLIB itself allows two seconds for the exact lookup.
    /// </summary>
    public static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The best fitting track with lyrics, or <c>null</c>; <paramref name="instrumental"/> tells whether a fitting
    /// track without lyrics is marked as instrumental.
    /// </summary>
    public static LrclibRecord? Choose(IReadOnlyList<LrclibRecord> records, LrclibQuery query, out bool instrumental)
    {
        LrclibRecord[] fitting = [.. records.Where(record => Fits(record, query))];
        LrclibRecord? best = fitting
            .Where(record => record.HasLyrics && !record.Instrumental)
            .OrderByDescending(record => record.HasSyncedLyrics)
            .ThenByDescending(record => SameAlbum(record, query))
            .ThenBy(record => Difference(record, query))
            .FirstOrDefault();
        instrumental = best is null && fitting.Any(record => record.Instrumental);
        return best;
    }

    private static bool Fits(LrclibRecord record, LrclibQuery query)
    {
        return Difference(record, query) <= DurationTolerance.TotalSeconds;
    }

    /// <summary>
    /// Difference of the durations in seconds; 0 if one of them is unknown.
    /// </summary>
    private static double Difference(LrclibRecord record, LrclibQuery query)
    {
        return record.Duration is { } seconds && query.Duration is { } duration
            ? Math.Abs(seconds - duration.TotalSeconds)
            : 0;
    }

    private static bool SameAlbum(LrclibRecord record, LrclibQuery query)
    {
        return query.AlbumName is not null
            && string.Equals(record.AlbumName?.Trim(), query.AlbumName.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
