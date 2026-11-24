namespace WaveMP.Data.Entities;

/// <summary>
/// Values of the tags an organize operation writes, or the values of these tags before. Only the fields named in
/// <see cref="Fields"/> count; a named field without value removes the tag. Stored as JSON.
/// </summary>
public sealed class OrganizeTagValues
{
    /// <summary>
    /// The fields that are written; every other field stays as it is.
    /// </summary>
    public List<OrganizeTagField> Fields { get; set; } = [];

    public string? Album { get; set; }

    /// <summary>
    /// All album artists; empty removes the tag.
    /// </summary>
    public List<string> AlbumArtists { get; set; } = [];

    public int? TrackNumber { get; set; }

    public int? TrackCount { get; set; }

    /// <summary>
    /// The number of the disc; removing it removes the number of discs as well.
    /// </summary>
    public int? DiscNumber { get; set; }

    /// <summary>
    /// The number of discs, written together with <see cref="DiscNumber"/> (only next to a number); used to put back
    /// the values of a backup.
    /// </summary>
    public int? DiscCount { get; set; }

    public int? Year { get; set; }

    /// <summary>
    /// All genres; empty removes the tag.
    /// </summary>
    public List<string> Genres { get; set; } = [];

    /// <summary>
    /// Whether no field is written.
    /// </summary>
    public bool IsEmpty => Fields.Count == 0;

    public bool Has(OrganizeTagField field)
    {
        return Fields.Contains(field);
    }
}
