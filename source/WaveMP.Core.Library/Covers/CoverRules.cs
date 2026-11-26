using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Which cover rules apply. The rule of the songs (<see cref="CoverPriority"/>): the most specific level that sets one
/// wins (the exception of the track, then the exception of its album, then the global setting); a level without a rule
/// of its own (<c>null</c>) inherits the next one. The rules of albums and tracks travel with their covers
/// (<see cref="CoverCandidateRow.AlbumCoverPriority"/> and <see cref="CoverCandidateRow.TrackCoverPriority"/>). What an
/// album shows without a cover of its own (<see cref="AlbumCoverFallback"/>): the exception of the album, otherwise the
/// global setting.
/// </summary>
public static class CoverRules
{
    public static CoverPriority Effective(CoverPriority global, CoverPriority? album, CoverPriority? track)
    {
        return track ?? album ?? global;
    }

    public static AlbumCoverFallback EffectiveFallback(AlbumCoverFallback global, AlbumCoverFallback? album)
    {
        return album ?? global;
    }

    /// <summary>
    /// The rule that applies to the songs of an album without a rule of their own: the exception of the album,
    /// otherwise the global one.
    /// </summary>
    public static CoverPriority ForAlbum(CoverPriority global, IEnumerable<CoverCandidateRow> candidatesOfAlbum)
    {
        return Effective(global, AlbumRuleOf(candidatesOfAlbum), track: null);
    }

    /// <summary>
    /// The rule that applies to a track: its own, otherwise the one of its album, otherwise the global one.
    /// </summary>
    public static CoverPriority ForTrack(
        Guid trackId,
        CoverPriority global,
        IEnumerable<CoverCandidateRow> candidates)
    {
        CoverCandidateRow[] rows = [.. candidates];
        return Effective(global, AlbumRuleOf(rows), TrackRuleOf(trackId, rows));
    }

    /// <summary>
    /// The rule of the album that the covers belong to; all covers of one album carry the same.
    /// </summary>
    public static CoverPriority? AlbumRuleOf(IEnumerable<CoverCandidateRow> candidates)
    {
        return candidates
            .Select(candidate => candidate.AlbumCoverPriority)
            .FirstOrDefault(rule => rule is not null);
    }

    /// <summary>
    /// The rule of the track, read from its covers; <c>null</c> if it has none or no covers of its own.
    /// </summary>
    public static CoverPriority? TrackRuleOf(Guid trackId, IEnumerable<CoverCandidateRow> candidates)
    {
        return candidates
            .Where(candidate => candidate.TrackId == trackId)
            .Select(candidate => candidate.TrackCoverPriority)
            .FirstOrDefault(rule => rule is not null);
    }
}
