namespace WaveMP.Data.Entities;

/// <summary>
/// How the scan removes the name of the artist from the start of a title tag (<c>"Artist - Title"</c> becomes
/// <c>"Title"</c>). It follows the layout template of the files: a file-based template such as
/// <c>{Artist} - {Title}</c> repeats the artist in every file name, so such title tags are common. Only the shown title
/// changes; nothing is written into the files. Stored as its name.
/// </summary>
public enum TitleCleaningMode
{
    /// <summary>
    /// Titles are kept as the tags have them.
    /// </summary>
    None,

    /// <summary>
    /// Folder-based layout: the prefix is removed only if it equals the artist tag exactly.
    /// </summary>
    ExactArtistTag,

    /// <summary>
    /// File-based layout: the prefix is removed if it equals the artist tag (or, without one, the artist of the path)
    /// without regard to case and white space.
    /// </summary>
    LooseArtist,
}
