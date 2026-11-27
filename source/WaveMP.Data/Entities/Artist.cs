namespace WaveMP.Data.Entities;

/// <summary>
/// An artist of tracks or albums, unique by its normalized name. Created by the scan and removed once nothing refers
/// to it. When its songs moved to other artists because their artist or album artist was renamed (see
/// <see cref="ArtistMove"/>), its favorites, its first seen date and a list in the player started from it go to those
/// artists before it is removed; its uploaded picture, its online picture and its MusicBrainz artist go to the artist
/// that got most of its songs, each only if that artist has none, and rules of smart playlists that name it to that
/// artist too. Everything that refers to an artist stores its id; only the scrobble queue and tag backups keep the
/// name as text, since they hold what was played or what stood in the tags.
/// </summary>
public class Artist
{
    public Guid Id { get; set; }

    /// <summary>
    /// Name as first found in the media files, or as an admin typed it in the metadata editor.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The key the artist is found by (see <see cref="Texts.NameKeys"/>), e.g. <c>kflay</c> for "K.Flay"; carries the
    /// unique index and sorts the artists. Never shown.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// The key the library search finds the artist by (see <see cref="Texts.SearchKeys"/>), e.g. <c>pnk pink</c> for
    /// "P!nk". Set from <see cref="Name"/> whenever the artist is saved; never shown.
    /// </summary>
    public string SearchKey { get; set; } = string.Empty;

    public required DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>
    /// The picture in the folder of the artist (e.g. <c>Artist/artist.jpg</c>), set by the scan; it is shown before
    /// the uploaded and the online picture. Bound to the folder, so it does not follow the artist to another row: the
    /// scan finds the picture of the new artist in its own folder. Cleared by the database when the file is removed.
    /// </summary>
    public Guid? LocalPictureFileId { get; set; }

    public MediaFile? LocalPictureFile { get; set; }

    /// <summary>
    /// The MusicBrainz artist of the artist: from the tags of its songs (a single MusicBrainz artist id), or as an
    /// admin chose it in the online lookup of artist pictures, so the next lookup skips the choice. A scan finds an
    /// artist by this id before its name.
    /// </summary>
    public Guid? MusicBrainzArtistId { get; set; }

    /// <summary>
    /// The uploaded and the online picture, at most one of each kind.
    /// </summary>
    public List<ArtistPicture> Pictures { get; set; } = [];

    /// <summary>
    /// The further names the search finds the artist by (see <see cref="ArtistAlias"/>).
    /// </summary>
    public List<ArtistAlias> Aliases { get; set; } = [];
}
