using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Parsers;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Chooses the lyrics of LRCLIB that fit a track best: only tracks whose duration differs by at most
/// <see cref="DurationTolerance"/> (if both durations are known), tracks with the names of the track first (if they
/// are given), synchronized lyrics before plain ones, then tracks of the same album, then the closest duration. If
/// no track fits the duration, the lyrics of another version of the song (same title and artist, other duration) are
/// taken without their times (<see cref="ChooseOtherVersion"/>).
/// </summary>
public static class LrclibMatcher
{
    /// <summary>
    /// The version of these rules. Lookups that found nothing with an older version are looked up again. Version 2
    /// added the other versions, the search without additions and with the main artist, the free-text search and the
    /// artist from the file name.
    /// </summary>
    public const int Version = 2;

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
        return Choose(records, query, null, out instrumental);
    }

    /// <summary>
    /// The best fitting track with lyrics, those with the <paramref name="names"/> of the track first, or
    /// <c>null</c>; <paramref name="instrumental"/> tells whether a fitting track without lyrics is marked as
    /// instrumental.
    /// </summary>
    public static LrclibRecord? Choose(
        IReadOnlyList<LrclibRecord> records,
        LrclibQuery query,
        LyricsNames? names,
        out bool instrumental)
    {
        LrclibRecord[] fitting = [.. records.Where(record => FitsDuration(record, query.Duration))];
        LrclibRecord? best = fitting
            .Where(record => record.HasLyrics && !record.Instrumental)
            .OrderByDescending(record => names?.Matches(record) ?? false)
            .ThenByDescending(record => record.HasSyncedLyrics)
            .ThenByDescending(record => SameAlbum(record, query))
            .ThenBy(record => Difference(record, query.Duration))
            .FirstOrDefault();
        instrumental = best is null && fitting.Any(record => record.Instrumental);
        return best;
    }

    /// <summary>
    /// The lyrics of another version of the song: of the tracks with lyrics whose duration does not fit but whose
    /// title and artist match the <paramref name="names"/> of the track, the one of the same album, then of the
    /// closest duration; <c>null</c> if there is none.
    /// </summary>
    public static LrclibRecord? ChooseOtherVersion(
        IEnumerable<LrclibRecord> records,
        LrclibQuery query,
        LyricsNames names)
    {
        return records
            .Where(record => record.HasLyrics && !record.Instrumental && names.Matches(record))
            .OrderByDescending(record => SameAlbum(record, query))
            .ThenBy(record => Difference(record, query.Duration))
            .FirstOrDefault();
    }

    /// <summary>
    /// Whether the duration of the track of LRCLIB differs by at most <see cref="DurationTolerance"/>; always true if
    /// one of the durations is unknown.
    /// </summary>
    public static bool FitsDuration(LrclibRecord record, TimeSpan? duration)
    {
        return Difference(record, duration) <= DurationTolerance.TotalSeconds;
    }

    /// <summary>
    /// The lyrics to keep of a track: the synchronized ones if they fit, otherwise the plain ones; for another version
    /// (<paramref name="otherVersion"/>) only the plain lines, read from the synchronized lyrics if the track has no
    /// plain ones. <c>null</c> without lyrics.
    /// </summary>
    public static string? LyricsOf(LrclibRecord record, bool otherVersion)
    {
        if (!otherVersion)
        {
            return record.Lyrics;
        }

        if (!string.IsNullOrWhiteSpace(record.PlainLyrics))
        {
            return record.PlainLyrics;
        }

        return record.SyncedLyrics is { } synced && LrcParser.Parse(synced) is { IsEmpty: false } document
            ? LyricsTexts.ToPlain(document)
            : null;
    }

    /// <summary>
    /// Difference of the durations in seconds; 0 if one of them is unknown.
    /// </summary>
    private static double Difference(LrclibRecord record, TimeSpan? duration)
    {
        return record.Duration is { } seconds && duration is { } known
            ? Math.Abs(seconds - known.TotalSeconds)
            : 0;
    }

    private static bool SameAlbum(LrclibRecord record, LrclibQuery query)
    {
        return query.AlbumName is not null
            && string.Equals(record.AlbumName?.Trim(), query.AlbumName.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
