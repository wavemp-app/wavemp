using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The ordered lists of covers that the library shows, e.g. to prepare them in advance. For an album these are the list
/// of the album, the list of every track with covers of its own and the list of its tracks without a cover of their
/// own; identical lists come only once. Every list follows the most specific cover rule (<see cref="CoverRules"/>) and
/// the order of <see cref="CoverResolver"/>.
/// </summary>
public static class CoverChoices
{
    /// <summary>
    /// The lists of an album from the covers of the album and of its tracks.
    /// </summary>
    /// <param name="global">The global rule of the songs of the library settings.</param>
    /// <param name="candidatesOfAlbum">The covers of the album and of its tracks with their rules.</param>
    public static IReadOnlyList<IReadOnlyList<CoverCandidateRow>> ForAlbum(
        CoverPriority global,
        IEnumerable<CoverCandidateRow> candidatesOfAlbum)
    {
        CoverCandidateRow[] candidates = [.. candidatesOfAlbum];
        List<IReadOnlyList<CoverCandidateRow>> lists = [CoverResolver.OrderForAlbum(candidates)];
        foreach (Guid trackId in candidates
            .Select(candidate => candidate.TrackId)
            .OfType<Guid>()
            .Distinct()
            .Order())
        {
            lists.Add(ForTrack(trackId, global, candidates));
        }

        // The tracks without a cover of their own share one list: the covers of the album, by the rule of the album.
        lists.Add(CoverResolver.OrderForTrack(Guid.Empty, CoverRules.ForAlbum(global, candidates), candidates));

        HashSet<string> seen = new(StringComparer.Ordinal);
        return [.. lists.Where(list => list.Count > 0 && seen.Add(KeyOf(list)))];
    }

    /// <summary>
    /// The list of a track from its covers and the covers of its album, by the rules the covers carry.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="global">The global rule of the songs of the library settings.</param>
    /// <param name="candidates">The covers of the track and of its album.</param>
    public static IReadOnlyList<CoverCandidateRow> ForTrack(
        Guid trackId,
        CoverPriority global,
        IEnumerable<CoverCandidateRow> candidates)
    {
        CoverCandidateRow[] rows = [.. candidates];
        return CoverResolver.OrderForTrack(trackId, CoverRules.ForTrack(trackId, global, rows), rows);
    }

    /// <summary>
    /// Identifies a list by the pictures it shows in their order, independent of the covers that refer to them.
    /// </summary>
    public static string KeyOf(IEnumerable<CoverCandidateRow> ordered)
    {
        return string.Join(
            ';',
            ordered.Select(candidate =>
                candidate.MediaFileId.ToString("N") + CoverSourceCodes.Of(candidate.Source)));
    }
}
