namespace WaveMP.Data.Entities;

/// <summary>
/// Why an item of an organize operation cannot run. Stored as its name.
/// </summary>
public enum OrganizeConflict
{
    /// <summary>
    /// Another file already has the new name in the target folder; names are compared without regard to case and
    /// Unicode normalization.
    /// </summary>
    TargetExists,

    /// <summary>
    /// Another item of the operation would get the same name.
    /// </summary>
    DuplicateTarget,

    /// <summary>
    /// The new path is longer than the library allows.
    /// </summary>
    PathTooLong,

    /// <summary>
    /// A folder of the old or new path is a symbolic link or junction, or the path leaves the media folder.
    /// </summary>
    UnsafePath,

    /// <summary>
    /// A file of the song is missing or changed since the library read it.
    /// </summary>
    SourceMissing,

    /// <summary>
    /// The audio file whose tags change cannot be written.
    /// </summary>
    NotWritable,

    /// <summary>
    /// The song has no audio file, so its tags cannot be written.
    /// </summary>
    NoAudio,

    /// <summary>
    /// The media source of the song is inactive or gone.
    /// </summary>
    SourceInactive,

    /// <summary>
    /// The album folder already has another file with the name of the cover.
    /// </summary>
    CoverExists,

    /// <summary>
    /// A file was changed or moved again since the operation that is undone.
    /// </summary>
    ChangedSince,
}
