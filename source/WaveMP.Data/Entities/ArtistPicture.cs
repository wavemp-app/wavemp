namespace WaveMP.Data.Entities;

/// <summary>
/// A picture of an artist kept in the data folder: one an admin uploaded or one an admin took over from the online
/// lookup. Each artist has at most one per <see cref="Kind"/>. The files lie in a folder named after <see cref="Id"/>,
/// never after the artist, and are re-encoded, so a new picture gets a new id. Removed with its artist; the files stay
/// until the clean-up. When the artist is removed because its songs moved to other artists, the picture goes to the
/// artist that got most of them, unless that artist has one of the same kind already (see
/// <see cref="Repositories.ILibraryScanRepository.DeleteOrphansAsync"/>). All texts are plain text; addresses are
/// absolute https addresses.
/// </summary>
public class ArtistPicture
{
    /// <summary>
    /// Id of the picture, also the name of the folder of its files.
    /// </summary>
    public Guid Id { get; set; }

    public required Guid ArtistId { get; set; }

    public Artist? Artist { get; set; }

    public required ArtistPictureKind Kind { get; set; }

    /// <summary>
    /// When the picture was stored; a newer picture of the same kind replaces the row.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Width of the largest stored size in pixels.
    /// </summary>
    public required int Width { get; set; }

    /// <summary>
    /// Height of the largest stored size in pixels.
    /// </summary>
    public required int Height { get; set; }

    /// <summary>
    /// The service of an online picture; <c>null</c> for an uploaded picture.
    /// </summary>
    public ArtistPictureProvider? Provider { get; set; }

    /// <summary>
    /// The MusicBrainz artist the online picture was found for.
    /// </summary>
    public Guid? MusicBrainzArtistId { get; set; }

    /// <summary>
    /// Author of the online picture as plain text, if the service names one.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Short name of the license of the online picture (e.g. <c>CC BY-SA 4.0</c>), if the service names one.
    /// </summary>
    public string? License { get; set; }

    /// <summary>
    /// Address of the license text.
    /// </summary>
    public string? LicenseUrl { get; set; }

    /// <summary>
    /// Address of the page of the picture at the service (the file page on Wikimedia Commons, the artist page on
    /// fanart.tv).
    /// </summary>
    public string? SourceUrl { get; set; }
}
