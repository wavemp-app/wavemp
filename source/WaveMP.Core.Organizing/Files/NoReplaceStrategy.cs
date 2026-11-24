namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// How <see cref="NoReplaceFileMover"/> moves files. Every strategy falls back to the next one where the file system
/// does not support it.
/// </summary>
public enum NoReplaceStrategy
{
    /// <summary>
    /// A rename without replace: <c>MoveFileEx</c> on Windows, <c>renameat2(RENAME_NOREPLACE)</c> on Linux; falls
    /// back to <see cref="Link"/> where the call is missing or not supported.
    /// </summary>
    Rename,

    /// <summary>
    /// A hard link at the target (fails if it exists), then the source name is removed (Unix only); falls back to
    /// <see cref="Copy"/> across volumes and on file systems without hard links.
    /// </summary>
    Link,

    /// <summary>
    /// A copy into a target created exclusively, flushed to disk and checked, then the source is deleted.
    /// </summary>
    Copy,
}
