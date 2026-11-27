namespace WaveMP.Data.Entities;

/// <summary>
/// A further name of an artist that the library search finds it by, e.g. "Alecia Moore" or "Pink" for "P!nk". The
/// search shows the name of the artist, never the alias. Aliases only serve the search: the scan never finds or joins
/// artists by them. Each artist has every normalized name (see <see cref="Texts.NameKeys"/>) at most once. Removed
/// with its artist; when the artist is removed because its songs moved to other artists, its aliases go to the artist
/// that got most of them (see <see cref="Repositories.ILibraryScanRepository.DeleteOrphansAsync"/>).
/// </summary>
public class ArtistAlias
{
    public Guid Id { get; set; }

    public required Guid ArtistId { get; set; }

    public Artist? Artist { get; set; }

    /// <summary>
    /// The alias as the admin typed it or MusicBrainz names it.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The normalized alias (see <see cref="Texts.NameKeys"/>); carries the unique index per artist.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// The key the search finds the alias by (see <see cref="Texts.SearchKeys"/>). Set from <see cref="Name"/>
    /// whenever the alias is saved.
    /// </summary>
    public string SearchKey { get; set; } = string.Empty;

    public required ArtistAliasSource Source { get; set; }

    /// <summary>
    /// Whether an admin removed the alias. A removed alias of MusicBrainz is kept, so the next lookup does not add it
    /// again; the search and the list of aliases leave it out. An admin adding it again brings it back.
    /// </summary>
    public bool IsRemoved { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
