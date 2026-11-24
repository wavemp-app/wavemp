namespace WaveMP.Data.Entities;

/// <summary>
/// Something to check in an item of an organize operation; the item still runs. Stored as its name.
/// </summary>
public enum OrganizeWarning
{
    /// <summary>
    /// At the new place, the library reads another title, artist, album or album artist, e.g. for files without tags
    /// whose metadata come from their path.
    /// </summary>
    MetadataChanges,

    /// <summary>
    /// The library already has an album with the title and album artist of the own album; the songs join it.
    /// </summary>
    AlbumMerges,

    /// <summary>
    /// The template has no album folder, so the cover of the own album is not stored next to the songs.
    /// </summary>
    CoverNotWritten,

    /// <summary>
    /// The album folder already has another cover picture, which may be shown instead.
    /// </summary>
    OtherCoverExists,

    /// <summary>
    /// The tags were changed since the operation that is undone; they are kept.
    /// </summary>
    TagsChangedSince,
}
