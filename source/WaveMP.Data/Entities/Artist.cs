namespace WaveMP.Data.Entities;

/// <summary>
/// An artist of tracks or albums, unique by its normalized name. Created by the scan and removed once nothing refers
/// to it. When its songs moved to other artists because their artist or album artist was renamed (see
/// <see cref="ArtistMove"/>), its favorites, its first seen date and a list in the player started from it go to those
/// artists before it is removed. Smart playlist rules, the scrobble queue and tag backups hold the artist name as text,
/// not this row, and are not changed by a rename.
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
}
