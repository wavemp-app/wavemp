namespace WaveMP.Data.Entities;

/// <summary>
/// A genre of tracks, unique by its normalized name (see <see cref="Texts.NameKeys"/>). Created by the scan for the
/// first genre of a track and removed once no track refers to it; tracks refer to it by its id. Rules of smart
/// playlists that name the genre keep its id, so they follow a new spelling of the genre.
/// </summary>
public class Genre
{
    public Guid Id { get; set; }

    /// <summary>
    /// Name as first found in the tags, or as an admin typed it in the metadata editor.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The key the genre is found by (see <see cref="Texts.NameKeys"/>); carries the unique index. Never shown.
    /// </summary>
    public required string NormalizedName { get; set; }
}
