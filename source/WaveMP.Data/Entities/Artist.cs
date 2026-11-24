namespace WaveMP.Data.Entities;

/// <summary>
/// An artist of tracks or albums, unique by its normalized name. Created by the scan and removed once nothing refers
/// to it. When its songs moved to other artists because their artist or album artist was renamed (see
/// <see cref="ArtistMove"/>), its favorites, its first seen date and a list in the player started from it go to those
/// artists before it is removed; its uploaded picture, its online picture and its MusicBrainz artist go to the artist
/// that got most of its songs, each only if that artist has none. Smart playlist rules, the scrobble queue and tag
/// backups hold the artist name as text, not this row, and are not changed by a rename.
/// </summary>
public class Artist
{
    public Guid Id { get; set; }

    /// <summary>
    /// Name as first found in the media files, or as an admin typed it in the metadata editor.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Name"/> with normalized white space; carries the unique index.
    /// </summary>
    public required string NormalizedName { get; set; }

    public required DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>
    /// The picture in the folder of the artist (e.g. <c>Artist/artist.jpg</c>), set by the scan; it is shown before
    /// the uploaded and the online picture. Bound to the folder, so it does not follow the artist to another row: the
    /// scan finds the picture of the new artist in its own folder. Cleared by the database when the file is removed.
    /// </summary>
    public Guid? LocalPictureFileId { get; set; }

    public MediaFile? LocalPictureFile { get; set; }

    /// <summary>
    /// The MusicBrainz artist an admin chose in the online lookup of artist pictures, so the next lookup skips the
    /// choice.
    /// </summary>
    public Guid? MusicBrainzArtistId { get; set; }

    /// <summary>
    /// The uploaded and the online picture, at most one of each kind.
    /// </summary>
    public List<ArtistPicture> Pictures { get; set; } = [];
}
