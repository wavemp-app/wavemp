namespace WaveMP.Data.Entities;

/// <summary>
/// A cover an admin applied from an online search for an album or a track, kept in the data folder because the media
/// folder could not be written. It is shown before the other covers of its album or track. Exactly one of
/// <see cref="AlbumId"/> and <see cref="TrackId"/> is set, and each album and track has at most one. Removed with its
/// album or track; the file stays until the next online cover of the same owner or the cleanup on start. The cover of
/// an album that is removed because its tracks moved goes to the album that got most of them, unless that album has
/// one already (see <see cref="Repositories.ILibraryScanRepository.DeleteOrphansAsync"/>).
/// </summary>
public class OnlineCover
{
    public Guid Id { get; set; }

    public Guid? AlbumId { get; set; }

    public Album? Album { get; set; }

    public Guid? TrackId { get; set; }

    public Track? Track { get; set; }

    /// <summary>
    /// Name of the picture in the folder of the online covers, e.g. <c>0193….jpg</c>.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Type of the picture, recognized by its content (e.g. <c>image/jpeg</c>).
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// Size of the picture in bytes.
    /// </summary>
    public required long Size { get; set; }

    /// <summary>
    /// The release of MusicBrainz the cover belongs to.
    /// </summary>
    public required Guid ReleaseId { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
