using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.OnlineMetadata.Covers;

/// <summary>
/// Where a cover from the online search goes, following the cover rules. Exactly one of <paramref name="AlbumId"/> and
/// <paramref name="TrackId"/> is set; it owns the cover. The cover of an album is always kept in the data folder by the
/// id of the album, so the folder of the target is empty; the cover of a song is a picture with the base name of the
/// song next to it (<c>Song.jpg</c>), or in the data folder if that folder cannot be written.
/// </summary>
/// <param name="AlbumId">The album of an album cover.</param>
/// <param name="TrackId">The song of a song cover.</param>
/// <param name="SourceId">The media source of the folder of a song.</param>
/// <param name="DirectoryPath">The folder of a song relative to the folder of the source.</param>
/// <param name="BaseName">The name of the picture of a song without extension.</param>
public sealed record CoverTarget(Guid? AlbumId, Guid? TrackId, Guid SourceId, string DirectoryPath, string BaseName)
{
    /// <summary>
    /// The cover of a song: the cover of its album if the cover rule that applies to the song shows the album cover
    /// first (and the song has an album), otherwise its own.
    /// </summary>
    public static CoverTarget ForTrack(OnlineTrackRow track, CoverPriority global)
    {
        CoverPriority rule = CoverRules.Effective(global, track.AlbumCoverPriority, track.TrackCoverPriority);
        return rule == CoverPriority.AlbumFirst && track.AlbumId is { } albumId
            ? ForAlbum(albumId)
            : new CoverTarget(null, track.TrackId, track.SourceId, track.DirectoryPath, track.BaseName);
    }

    /// <summary>
    /// The cover of an album, kept in the data folder by the id of the album.
    /// </summary>
    public static CoverTarget ForAlbum(Guid albumId)
    {
        return new CoverTarget(albumId, null, Guid.Empty, string.Empty, string.Empty);
    }
}
