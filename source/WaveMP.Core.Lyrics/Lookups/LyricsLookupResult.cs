using System.Diagnostics.CodeAnalysis;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// The stored result of the lookup of a track, or why there is none.
/// </summary>
public sealed record LyricsLookupResult(LyricsLookupStatus Status, LyricsDownloadRow? Lookup = null)
{
    [MemberNotNullWhen(true, nameof(Lookup))]
    public bool IsFound => Status == LyricsLookupStatus.Found && Lookup is not null;

    /// <summary>
    /// Whether the stored result found no lyrics (not found or instrumental), so an admin can forget it.
    /// </summary>
    public bool IsForgettable => Lookup is { } lookup && IsForgettableStatus(lookup.Status);

    public static LyricsLookupResult TrackNotFound { get; } = new(LyricsLookupStatus.TrackNotFound);

    public static LyricsLookupResult NotLookedUp { get; } = new(LyricsLookupStatus.NotLookedUp);

    public static LyricsLookupResult Found(LyricsDownloadRow lookup)
    {
        return new LyricsLookupResult(LyricsLookupStatus.Found, lookup);
    }

    /// <summary>
    /// Whether a lookup with this status found no lyrics (not found or instrumental); only such results are
    /// forgotten.
    /// </summary>
    public static bool IsForgettableStatus(LyricsDownloadStatus status)
    {
        return status is LyricsDownloadStatus.NotFound or LyricsDownloadStatus.Instrumental;
    }
}
