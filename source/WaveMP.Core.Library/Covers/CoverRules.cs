using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Which cover rule applies: the most specific level that sets one wins (track, then album, then the global setting).
/// A level without a rule of its own (<c>null</c>) inherits the next one. The rules of albums and tracks travel with
/// their covers (<see cref="CoverCandidateRow.AlbumCoverPriority"/> and
/// <see cref="CoverCandidateRow.TrackCoverPriority"/>).
/// </summary>
public static class CoverRules
{
    public static CoverPriority Effective(CoverPriority global, CoverPriority? album, CoverPriority? track)
    {
        return track ?? album ?? global;
    }

    /// <summary>
    /// The rule that applies to an album: its own, otherwise the global one.
    /// </summary>
    public static CoverPriority ForAlbum(CoverPriority global, IEnumerable<CoverCandidateRow> candidatesOfAlbum)
    {
        return Effective(global, AlbumRuleOf(candidatesOfAlbum), track: null);
    }

    /// <summary>
    /// The rule that applies to a track: its own, otherwise the one of its album, otherwise the global one. A track
    /// without covers of its own shows the cover of its album first under every rule, so its own rule only matters if
    /// it has covers.
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
