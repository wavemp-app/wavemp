using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;

namespace WaveMP.Core.Backups.Files;

internal sealed class BackupStore(IOptions<BackupOptions> options, ILogger<BackupStore> logger) : IBackupStore
{
    /// <summary>
    /// Highest number added to the id of exports of the same second.
    /// </summary>
    internal const int MaxSequence = 999;

    public string RootPath { get; } = options.Value.RootPath;

    public IReadOnlyList<BackupFile> List()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        List<(BackupFile File, int Sequence)> files = [];
        foreach (string path in Directory.EnumerateFiles(RootPath, "*" + BackupFileNames.Extension))
        {
            if (Read(path) is { } file)
            {
                files.Add(file);
            }
        }

        return [.. files
            .OrderByDescending(entry => entry.File.CreatedAt)
            .ThenByDescending(entry => entry.Sequence)
            .Select(entry => entry.File)];
    }

    public BackupFile? Find(string id)
    {
        return BackupFileNames.TryParseId(id, out _, out _, out _) ? Read(PathOf(id))?.File : null;
    }

    public bool Delete(string id)
    {
        if (Find(id) is null)
        {
            return false;
        }

        try
        {
            File.Delete(PathOf(id));
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    public string Prepare(DateTimeOffset createdAt, BackupTrigger trigger)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            foreach (string partial in Directory.EnumerateFiles(RootPath, "*" + BackupFileNames.PartialExtension))
            {
                File.Delete(partial);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new BackupFailedException(
                BackupError.FolderUnwritable,
                $"The backup folder {RootPath} cannot be written: {exception.Message}",
                exception);
        }

        for (int sequence = 1; sequence <= MaxSequence; sequence++)
        {
            string id = BackupFileNames.CreateId(createdAt, trigger, sequence);
            if (!File.Exists(PathOf(id)))
            {
                return id;
            }
        }

        throw new BackupFailedException(
            BackupError.FolderUnwritable,
            $"The backup folder {RootPath} has no free name for an export of {createdAt:O}.");
    }

    public string PartialPathOf(string id)
    {
        return PathOf(id) + BackupFileNames.PartialExtension;
    }

    public BackupFile Complete(string id)
    {
        try
        {
            File.Move(PartialPathOf(id), PathOf(id));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new BackupFailedException(
                BackupError.FolderUnwritable,
                $"The export {id} cannot be saved in {RootPath}: {exception.Message}",
                exception);
        }

        return Read(PathOf(id))?.File
            ?? throw new InvalidOperationException($"The export {id} disappeared after it was saved.");
    }

    public void Discard(string id)
    {
        try
        {
            File.Delete(PartialPathOf(id));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot remove the unfinished export {BackupId}", id);
        }
    }

    public IReadOnlyList<BackupFile> ApplyRetention(int keep)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keep);

        List<BackupFile> deleted = [];
        foreach (BackupFile file in List().Skip(keep))
        {
            try
            {
                File.Delete(PathOf(file.Id));
                deleted.Add(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Cannot delete the old export {BackupFile}", file.FileName);
            }
        }

        return deleted;
    }

    private string PathOf(string id)
    {
        return Path.Combine(RootPath, id + BackupFileNames.Extension);
    }

    private static (BackupFile File, int Sequence)? Read(string path)
    {
        string fileName = Path.GetFileName(path);
        if (BackupFileNames.IdOfFileName(fileName) is not { } id
            || !BackupFileNames.TryParseId(
                id,
                out DateTimeOffset createdAt,
                out BackupTrigger trigger,
                out int sequence))
        {
            return null;
        }

        FileInfo info = new(path);
        return info.Exists ? (new BackupFile(id, fileName, createdAt, info.Length, trigger), sequence) : null;
    }
}
