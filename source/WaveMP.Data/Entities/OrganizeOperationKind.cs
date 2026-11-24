namespace WaveMP.Data.Entities;

/// <summary>
/// What an organize operation does. Stored as its name.
/// </summary>
public enum OrganizeOperationKind
{
    /// <summary>
    /// Applies an own album: writes its tags and moves its songs by the layout template.
    /// </summary>
    OwnAlbum,

    /// <summary>
    /// Moves the songs of the library (or of one media source) by the layout template; writes no tags.
    /// </summary>
    Library,

    /// <summary>
    /// Undoes an earlier operation.
    /// </summary>
    Undo,
}
