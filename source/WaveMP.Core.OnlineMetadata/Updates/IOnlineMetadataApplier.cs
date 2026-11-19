using WaveMP.Core.Metadata.Tags;

namespace WaveMP.Core.OnlineMetadata.Updates;

/// <summary>
/// Applies the fields of a match the admin confirmed: tags through the metadata editor (audio files only, the current
/// tags saved in <c>tag_backups</c> first) and the cover of the release, stored following the cover rules. The cover
/// is downloaded before anything is written, so an unreachable Cover Art Archive changes nothing.
/// </summary>
public interface IOnlineMetadataApplier
{
    /// <summary>
    /// Applies tags and the cover of a release to a song.
    /// </summary>
    /// <param name="trackId">Id of the song.</param>
    /// <param name="changes">The tags to write; empty for none.</param>
    /// <param name="coverReleaseId">The release whose front cover becomes the cover; <c>null</c> for none.</param>
    /// <param name="cancellationToken">Aborts before anything is written.</param>
    Task<TrackApplyResult> ApplyToTrackAsync(
        Guid trackId,
        TagChanges changes,
        Guid? coverReleaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the album fields (album, album artist, year, genre) to every song of an album that can be written, and
    /// the cover of a release as cover of the album. Other fields of <paramref name="changes"/> are ignored.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="changes">The tags to write; empty for none.</param>
    /// <param name="coverReleaseId">The release whose front cover becomes the cover; <c>null</c> for none.</param>
    /// <param name="cancellationToken">Aborts before anything is written.</param>
    Task<AlbumApplyResult> ApplyToAlbumAsync(
        Guid albumId,
        TagChanges changes,
        Guid? coverReleaseId,
        CancellationToken cancellationToken = default);
}
