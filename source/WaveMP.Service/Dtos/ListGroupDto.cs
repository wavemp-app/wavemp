namespace WaveMP.Service.Dtos;

/// <summary>
/// A group of a list: a run of consecutive entries with the same album, artist, index letter or year, in the order of
/// the list. Contains no display texts; clients name the group from its kind and data.
/// </summary>
public sealed class ListGroupDto
{
    public required ListGroupKindDto Kind { get; init; }

    /// <summary>
    /// Index of the first entry of the group in the list, starting at 0: the offset of the list at which the group
    /// starts.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Number of entries of the group.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// The album of a group of the kind Album; null otherwise.
    /// </summary>
    public AlbumReferenceDto? Album { get; init; }

    /// <summary>
    /// The artist of a group of the kind Artist; null otherwise.
    /// </summary>
    public ArtistReferenceDto? Artist { get; init; }

    /// <summary>
    /// Index letter of the text the list is sorted by, taken from the first entry of the group: "A" to "Z" (the
    /// first character without accents), "#" for digits, special characters and other scripts. Null if the list is
    /// not sorted by a text or the first entry has none.
    /// </summary>
    public string? Letter { get; init; }

    /// <summary>
    /// The year of a group of the kind Year; in the track list sorted by Year the year of the first track of the
    /// group. Null otherwise.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Whether the album of the group has a cover (GetAlbumCover); false for groups without album.
    /// </summary>
    public required bool HasCover { get; init; }
}
