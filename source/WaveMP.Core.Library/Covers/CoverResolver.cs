using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Orders the covers that may be shown for a track or an album, the best first.
/// <para>
/// A track shows only its own covers and the covers of its album, never the cover of another track: a cover the admin
/// selected for it comes first, then a picture file with its base name (e.g. <c>Song.jpg</c>), unless the track itself
/// has the rule to show the album cover first. Then the rule decides between the cover of the album (a picture in its
/// folder, or one the admin applied from the online search) and the other covers of the track (embedded pictures and
/// online covers); the other one is the fallback. A track without any of these shows no cover.
/// </para>
/// <para>
/// For an album the rule decides between its own cover and the covers of its tracks, which come in the order of the
/// tracks (disc and track number).
/// </para>
/// Within a group a cover the admin applied from the online search (kept in the data folder) comes first, then the
/// primary cover, picture files before embedded pictures.
/// </summary>
public static class CoverResolver
{
    /// <param name="trackId">Id of the track.</param>
    /// <param name="priority">The cover rule that applies to the track.</param>
    /// <param name="candidates">The covers of the track and of its album; covers of other tracks are left out.</param>
    /// <param name="ownFileFirst">Whether a picture file of the track comes before the cover of the album whatever
    /// the rule says; <c>false</c> if the track itself has the rule to show the album cover first.</param>
    public static IReadOnlyList<CoverCandidateRow> OrderForTrack(
        Guid trackId,
        CoverPriority priority,
        IEnumerable<CoverCandidateRow> candidates,
        bool ownFileFirst = true)
    {
        return Order(
            candidates.Where(candidate => candidate.AlbumId is not null || candidate.TrackId == trackId),
            candidate =>
            {
                if (candidate.AlbumId is not null)
                {
                    return priority == CoverPriority.AlbumFirst ? 2 : 3;
                }

                if (candidate.IsSelected)
                {
                    return 0;
                }

                if (ownFileFirst && candidate.Source == CoverSource.File)
                {
                    return 1;
                }

                return priority == CoverPriority.AlbumFirst ? 3 : 2;
            });
    }

    /// <summary>
    /// For an album the rule decides between its own cover and the covers of its tracks.
    /// </summary>
    public static IReadOnlyList<CoverCandidateRow> OrderForAlbum(
        CoverPriority priority,
        IEnumerable<CoverCandidateRow> candidates)
    {
        return Order(candidates, candidate =>
        {
            bool albumCover = candidate.AlbumId is not null;
            return albumCover == (priority == CoverPriority.AlbumFirst) ? 0 : 1;
        });
    }

    private static CoverCandidateRow[] Order(
        IEnumerable<CoverCandidateRow> candidates,
        Func<CoverCandidateRow, int> group)
    {
        return [.. candidates
            .OrderBy(group)
            .ThenBy(candidate => candidate.DiscNumber ?? int.MaxValue)
            .ThenBy(candidate => candidate.TrackNumber ?? int.MaxValue)
            .ThenBy(candidate => candidate.TrackTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.TrackId)
            .ThenBy(candidate => candidate.Source == CoverSource.Online ? 0 : 1)
            .ThenByDescending(candidate => candidate.IsPrimary)
            .ThenBy(candidate => candidate.Source == CoverSource.File ? 0 : 1)
            .ThenBy(candidate => candidate.CoverId)];
    }
}
