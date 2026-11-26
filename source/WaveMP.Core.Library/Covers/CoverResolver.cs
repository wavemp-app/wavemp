using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Orders the covers that may be shown for a track or an album, the best first. Covers come only from one level up:
/// a track shows its own cover or the cover of its album, never the picture of an artist; an album shows its own cover
/// (the picture of its album artist is a fallback of the delivery, see <see cref="CoverCandidates.FallbackArtistId"/>),
/// never the cover of another album.
/// <para>
/// The <b>own cover of a track</b> is a cover the admin selected for it, an online cover of the track and a picture
/// file with its base name (e.g. <c>Song.jpg</c>). The <b>cover of its album</b> is the online cover of the album, a
/// picture in the folder of the album (one that belongs to the album by the rules of the scan), then a picture
/// embedded in the files of the track and then one embedded in the files of the other tracks of the album, in the
/// order of the tracks. The rule of the track decides the order (<see cref="CoverPriority"/>): own cover, else album
/// cover; album cover, else own cover; or only the own cover. A cover the admin selected for the track comes first
/// under every rule.
/// </para>
/// <para>
/// The <b>own cover of an album</b> is its online cover, a picture in its folder and the pictures embedded in the files
/// of its tracks, in the order of the tracks. Pictures with the name of a track and the online covers of tracks are no
/// covers of the album.
/// </para>
/// Within a group the primary cover comes first, picture files before embedded pictures.
/// </summary>
public static class CoverResolver
{
    /// <param name="trackId">Id of the track.</param>
    /// <param name="priority">The cover rule that applies to the track.</param>
    /// <param name="candidates">The covers of the track and of its album (see
    /// <see cref="Data.Repositories.ICoverRepository.GetTrackCandidatesAsync"/>); covers of other tracks that are no
    /// covers of the album are left out.</param>
    public static IReadOnlyList<CoverCandidateRow> OrderForTrack(
        Guid trackId,
        CoverPriority priority,
        IEnumerable<CoverCandidateRow> candidates)
    {
        return Order(
            candidates.Where(candidate => KindForTrack(trackId, candidate) is { } kind
                && (kind != TrackKind.Album || priority != CoverPriority.TrackOnly)),
            candidate => KindForTrack(trackId, candidate) switch
            {
                TrackKind.Selected => 0,
                TrackKind.Own => priority == CoverPriority.AlbumFirst ? 2 : 1,
                _ => priority == CoverPriority.AlbumFirst ? 1 : 2,
            },
            candidate => candidate switch
            {
                { Source: CoverSource.Online } => 0,
                { AlbumId: not null } => 1,
                _ when candidate.TrackId == trackId => 2,
                _ => 3,
            });
    }

    /// <summary>
    /// The own covers of an album, the best first; the same for every rule of the album.
    /// </summary>
    public static IReadOnlyList<CoverCandidateRow> OrderForAlbum(IEnumerable<CoverCandidateRow> candidates)
    {
        return Order(
            candidates.Where(IsAlbumCover),
            _ => 0,
            candidate => candidate switch
            {
                { Source: CoverSource.Online } => 0,
                { AlbumId: not null } => 1,
                _ => 2,
            });
    }

    /// <summary>
    /// Whether a cover is a cover of its album: a picture in the folder of the album, its online cover or a picture
    /// embedded in the files of one of its tracks.
    /// </summary>
    public static bool IsAlbumCover(CoverCandidateRow candidate)
    {
        return candidate.AlbumId is not null || candidate.Source == CoverSource.Embedded;
    }

    /// <summary>
    /// Whether a cover is an own cover of its track: a picture with its name, a selected one or an online cover of the
    /// track.
    /// </summary>
    public static bool IsOwnTrackCover(CoverCandidateRow candidate)
    {
        return candidate.TrackId is not null && candidate.Source != CoverSource.Embedded;
    }

    private static TrackKind? KindForTrack(Guid trackId, CoverCandidateRow candidate)
    {
        // A cover the admin selected in the files of the track, also a picture embedded in them.
        if (candidate.TrackId == trackId && candidate.IsSelected)
        {
            return TrackKind.Selected;
        }

        if (IsAlbumCover(candidate))
        {
            return TrackKind.Album;
        }

        return candidate.TrackId == trackId ? TrackKind.Own : null;
    }

    private static CoverCandidateRow[] Order(
        IEnumerable<CoverCandidateRow> candidates,
        Func<CoverCandidateRow, int> group,
        Func<CoverCandidateRow, int> place)
    {
        return [.. candidates
            .OrderBy(group)
            .ThenBy(place)
            .ThenBy(candidate => candidate.DiscNumber ?? int.MaxValue)
            .ThenBy(candidate => candidate.TrackNumber ?? int.MaxValue)
            .ThenBy(candidate => candidate.TrackTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.TrackId)
            .ThenByDescending(candidate => candidate.IsPrimary)
            .ThenBy(candidate => candidate.Source == CoverSource.File ? 0 : 1)
            .ThenBy(candidate => candidate.CoverId)];
    }

    private enum TrackKind
    {
        Selected,
        Own,
        Album,
    }
}
