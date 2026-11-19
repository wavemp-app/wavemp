using WaveMP.Data.Queries;

namespace WaveMP.Data.Entities;

/// <summary>
/// A smart playlist of a user: rules that pick tracks from the library, a sort order and an optional limit. The tracks
/// are not stored; they are computed from the rules on every request, so the list always reflects the current library.
/// The smart playlist disappears with its user.
/// </summary>
public class SmartPlaylist
{
    public Guid Id { get; set; }

    public required Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Name of the smart playlist; names do not have to be unique (the same rules as for playlists).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Name"/> for sorting without regard to case.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// Whether a track has to meet all rules or at least one of them. Stored as its name.
    /// </summary>
    public required SmartPlaylistMatch Match { get; set; }

    /// <summary>
    /// The rules in the order of the user, stored as JSON (column type <c>jsonb</c>). Without rules every track of the
    /// library matches.
    /// </summary>
    public List<SmartPlaylistRule> Rules { get; set; } = [];

    /// <summary>
    /// Sort key of the tracks; ties are sorted by album, disc and track number. Stored as its name.
    /// </summary>
    public required SmartPlaylistSortField Sort { get; set; }

    /// <summary>
    /// Sort direction of the key. Stored as its name.
    /// </summary>
    public required SortDirection Direction { get; set; }

    /// <summary>
    /// Largest number of tracks, the first ones in the sort order; <c>null</c> for all matching tracks.
    /// </summary>
    public int? Limit { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the name or the rules changed for the last time.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; set; }
}
