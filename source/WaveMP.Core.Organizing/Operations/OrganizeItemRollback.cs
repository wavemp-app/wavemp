using Microsoft.Extensions.Logging;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Organizing.Operations;

/// <summary>
/// Puts an item of an organize operation back as it was before it ran, from what is on disk: a file that is no longer
/// at its old place but at its hidden in-between name or its new place goes back (without replacing anything), a cover
/// the item wrote is removed while it is unchanged, the tags are written back from the backup and the folders the item
/// created are removed while they are empty. Used when an item fails and, after a crash, on the next start. Every step
/// can be repeated.
/// </summary>
internal sealed class OrganizeItemRollback(INoReplaceFileMover mover, ITagWriter tagWriter, ILogger logger)
{
    /// <summary>
    /// The hidden name a file passes through while an undo removes it: it is only deleted once the undo of its item
    /// is recorded, so a failing or interrupted item can bring it back.
    /// </summary>
    public static string RemovalTempPath(OrganizeFileMove move)
    {
        string from = move.FromPath!;
        string? extension = FolderNames.ExtensionOf(FolderNames.FileNameOf(from));
        return FolderNames.Combine(
            FolderNames.DirectoryOf(from),
            OrganizePlanner.TemporaryPrefix + move.Id.ToString("N") + (extension is null ? string.Empty : "." + extension));
    }

    /// <summary>
    /// Puts the item back. Returns <c>false</c> if a file could not be put back; the item then stays as it is, so the
    /// next start tries again.
    /// </summary>
    /// <param name="rootPath">The folder of the media source.</param>
    /// <param name="item">The item with its journal.</param>
    /// <param name="moves">The moves to put back (all moves of the item after a crash).</param>
    /// <param name="backup">The backup of the tags taken by the item, if it took one.</param>
    /// <param name="coverSourcePath">The uploaded cover a cover item copied, to recognize its copy.</param>
    public bool PutBack(
        string rootPath,
        OrganizeItem item,
        IEnumerable<OrganizeFileMove> moves,
        TagBackup? backup,
        string? coverSourcePath)
    {
        bool complete = true;
        foreach (OrganizeFileMove move in moves.OrderByDescending(move => move.Sequence))
        {
            complete &= PutBack(rootPath, move, coverSourcePath);
        }

        if (backup is not null && item.TagChanges is { IsEmpty: false } changes)
        {
            complete &= RestoreTags(rootPath, backup, changes.Fields);
        }

        RemoveCreatedFolders(rootPath, item.CreatedFolders);
        return complete;
    }

    /// <summary>
    /// Removes the folders an item created (outermost first in the journal), innermost first, while they are empty.
    /// </summary>
    public static void RemoveCreatedFolders(string rootPath, IReadOnlyList<string> createdFolders)
    {
        foreach (string folder in createdFolders.Reverse())
        {
            if (!MediaPathGuard.TryResolve(rootPath, folder, isTarget: false, out string fullPath)
                || !Directory.Exists(fullPath))
            {
                continue;
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(fullPath).Any())
                {
                    Directory.Delete(fullPath, recursive: false);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be removed stays; it is empty and harmless.
            }
        }
    }

    private bool PutBack(string rootPath, OrganizeFileMove move, string? coverSourcePath)
    {
        if (move.FromPath is not { } from)
        {
            return RemoveCoverCopy(rootPath, move, coverSourcePath);
        }

        if (!MediaPathGuard.TryResolve(rootPath, from, isTarget: false, out string fromFull))
        {
            logger.LogError("The file {File} cannot be put back: its path is not safe", from);
            return false;
        }

        if (File.Exists(fromFull))
        {
            RemovePartialCopy(rootPath, from, fromFull, move.ToPath);
            return true;
        }

        string? temporary = move.ToPath is null ? RemovalTempPath(move) : move.TempPath;
        foreach (string? candidate in new[] { temporary, move.ToPath })
        {
            if (candidate is null
                || !MediaPathGuard.TryResolve(rootPath, candidate, isTarget: false, out string candidateFull)
                || !File.Exists(candidateFull))
            {
                continue;
            }

            FileMoveResult result = mover.Move(candidateFull, fromFull);
            if (result.IsMoved)
            {
                logger.LogInformation("The file {From} was put back to {To}", candidate, from);
                return true;
            }

            logger.LogError(
                "The file {From} cannot be put back to {To}: {Status} ({Error})",
                candidate,
                from,
                result.Status,
                result.ErrorNumber);
            return false;
        }

        logger.LogError("The file {File} is neither at its old nor at its new place", from);
        return false;
    }

    /// <summary>
    /// A move into another volume copies the file and deletes the original afterwards; a crash in between leaves the
    /// original and a (partial) copy. The copy is removed only if it is no other file: not the same name (a rename of
    /// the case only on a file system without regard to case) and its content is the beginning of the original or
    /// all of it.
    /// </summary>
    private void RemovePartialCopy(string rootPath, string from, string fromFull, string? to)
    {
        if (to is null || FolderNames.AreSame(from, to)
            || !MediaPathGuard.TryResolve(rootPath, to, isTarget: false, out string toFull)
            || !File.Exists(toFull))
        {
            return;
        }

        try
        {
            if (!IsBeginningOf(toFull, fromFull))
            {
                return;
            }

            File.Delete(toFull);
            logger.LogInformation("The unfinished copy {File} of {Original} was removed", to, from);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The unfinished copy {File} cannot be removed", to);
        }
    }

    private static bool IsBeginningOf(string copyPath, string originalPath)
    {
        using FileStream copy = new(copyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream original = new(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (copy.Length > original.Length)
        {
            return false;
        }

        byte[] left = new byte[81920];
        byte[] right = new byte[81920];
        while (true)
        {
            int read = copy.Read(left, 0, left.Length);
            if (read == 0)
            {
                return true;
            }

            original.ReadExactly(right, 0, read);
            if (!left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, read)))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Removes the copy of the cover of an own album, but only while it is unchanged: the same content as the uploaded
    /// cover, or (if that is gone) the size and change time of the copy.
    /// </summary>
    private bool RemoveCoverCopy(string rootPath, OrganizeFileMove move, string? coverSourcePath)
    {
        if (move.ToPath is not { } to
            || !MediaPathGuard.TryResolve(rootPath, to, isTarget: false, out string toFull)
            || !File.Exists(toFull))
        {
            return true;
        }

        try
        {
            FileInfo copy = new(toFull);
            bool unchanged = coverSourcePath is not null && File.Exists(coverSourcePath)
                ? copy.Length == new FileInfo(coverSourcePath).Length
                    && File.ReadAllBytes(toFull).AsSpan().SequenceEqual(File.ReadAllBytes(coverSourcePath))
                : new FileFingerprint(copy.Length, FileFingerprint.Truncate(copy.LastWriteTimeUtc))
                    .Matches(move.Size, move.ModifiedAt);
            if (!unchanged)
            {
                logger.LogWarning("The cover {File} changed since it was written and stays", to);
                return true;
            }

            File.Delete(toFull);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "The cover {File} cannot be removed", to);
            return false;
        }
    }

    private bool RestoreTags(string rootPath, TagBackup backup, IReadOnlyList<OrganizeTagField> fields)
    {
        if (!MediaPathGuard.TryResolve(rootPath, backup.RelativePath, isTarget: false, out string fullPath)
            || !File.Exists(fullPath))
        {
            logger.LogError("The tags of {File} cannot be put back: the file is missing", backup.RelativePath);
            return false;
        }

        TagWriteStatus status = tagWriter.Write(
            fullPath,
            MediaFileKind.Audio,
            OrganizeTags.ToTagChanges(OrganizeTags.FromBackup(fields, backup)));
        if (status != TagWriteStatus.Written)
        {
            logger.LogError("The tags of {File} cannot be put back: {Status}", backup.RelativePath, status);
            return false;
        }

        return true;
    }
}
