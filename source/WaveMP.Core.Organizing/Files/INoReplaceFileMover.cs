namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Moves and copies single files without ever replacing an existing file, also when another process creates the
/// target at the same moment: the check whether the target exists is part of the atomic operation of the file system
/// (a rename without replace, a hard link, or a file created exclusively). Folders are never moved, and the target
/// folder must exist.
/// </summary>
public interface INoReplaceFileMover
{
    /// <summary>
    /// Moves a file. On any failure the file stays at <paramref name="sourcePath"/> and nothing is left at
    /// <paramref name="targetPath"/>.
    /// </summary>
    FileMoveResult Move(string sourcePath, string targetPath);

    /// <summary>
    /// Copies a file into a new file at <paramref name="targetPath"/> created exclusively, with the change time of the
    /// source; the source stays. On any failure nothing is left at the target.
    /// </summary>
    FileMoveResult Copy(string sourcePath, string targetPath);
}
