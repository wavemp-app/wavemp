using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Moves files without replacing (see <see cref="INoReplaceFileMover"/>). By default the rename of the operating
/// system; tests choose a strategy to run every fallback on every system.
/// </summary>
internal sealed class NoReplaceFileMover : INoReplaceFileMover
{
    /// <summary>
    /// Windows: the target exists (<c>ERROR_FILE_EXISTS</c>, <c>ERROR_ALREADY_EXISTS</c>).
    /// </summary>
    private const int WindowsFileExists = unchecked((int)0x80070050);
    private const int WindowsAlreadyExists = unchecked((int)0x800700B7);

    private const int CopyBufferSize = 1 << 20;

    private static volatile bool _renameAt2Missing;

    private readonly NoReplaceStrategy _strategy;
    private readonly ILogger _logger;

    public NoReplaceFileMover(ILogger<NoReplaceFileMover> logger)
        : this(NoReplaceStrategy.Rename, logger)
    {
    }

    internal NoReplaceFileMover(NoReplaceStrategy strategy, ILogger? logger = null)
    {
        _strategy = strategy;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Called by the copy strategy right before the source is deleted, with the path of the copy; lets tests fail at
    /// the most delicate moment.
    /// </summary>
    internal Action<string>? BeforeSourceDelete { get; init; }

    public FileMoveResult Move(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return FileMoveResult.SourceMissing;
        }

        return _strategy switch
        {
            NoReplaceStrategy.Rename => Rename(sourcePath, targetPath),
            NoReplaceStrategy.Link => Link(sourcePath, targetPath),
            _ => CopyAndDelete(sourcePath, targetPath),
        };
    }

    public FileMoveResult Copy(string sourcePath, string targetPath)
    {
        return CopyExclusive(sourcePath, targetPath, deleteSource: false);
    }

    private FileMoveResult Rename(string sourcePath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return RenameOnWindows(sourcePath, targetPath);
        }

        if (!OperatingSystem.IsLinux() || _renameAt2Missing)
        {
            return Link(sourcePath, targetPath);
        }

        int error;
        try
        {
            error = UnixFileCalls.RenameNoReplace(sourcePath, targetPath);
        }
        catch (EntryPointNotFoundException)
        {
            _renameAt2Missing = true;
            return Link(sourcePath, targetPath);
        }

        return error switch
        {
            UnixFileCalls.Success => FileMoveResult.Moved,
            UnixFileCalls.Eexist => FileMoveResult.TargetExists,
            UnixFileCalls.Enoent when !File.Exists(sourcePath) => FileMoveResult.SourceMissing,
            UnixFileCalls.Exdev => CopyAndDelete(sourcePath, targetPath),
            UnixFileCalls.Enosys or UnixFileCalls.Einval or UnixFileCalls.Eopnotsupp => Link(sourcePath, targetPath),
            _ => Failed(sourcePath, targetPath, error),
        };
    }

    /// <summary>
    /// <c>File.Move</c> without overwrite is <c>MoveFileEx</c> without <c>MOVEFILE_REPLACE_EXISTING</c>: atomic on one
    /// volume, a copy that refuses an existing target across volumes.
    /// </summary>
    private FileMoveResult RenameOnWindows(string sourcePath, string targetPath)
    {
        try
        {
            File.Move(sourcePath, targetPath, overwrite: false);
            return FileMoveResult.Moved;
        }
        catch (FileNotFoundException) when (!File.Exists(sourcePath))
        {
            return FileMoveResult.SourceMissing;
        }
        catch (IOException exception) when (exception.HResult is WindowsFileExists or WindowsAlreadyExists)
        {
            return FileMoveResult.TargetExists;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Cannot move {Source} to {Target}", sourcePath, targetPath);
            return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
        }
    }

    private FileMoveResult Link(string sourcePath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // No hard links through .NET on Windows; the copy is the fallback there.
            return CopyAndDelete(sourcePath, targetPath);
        }

        int error = UnixFileCalls.Link(sourcePath, targetPath);
        switch (error)
        {
            case UnixFileCalls.Success:
                break;
            case UnixFileCalls.Eexist:
                return FileMoveResult.TargetExists;
            case UnixFileCalls.Enoent when !File.Exists(sourcePath):
                return FileMoveResult.SourceMissing;
            case UnixFileCalls.Exdev or UnixFileCalls.Eperm or UnixFileCalls.Eopnotsupp:
                return CopyAndDelete(sourcePath, targetPath);
            default:
                return Failed(sourcePath, targetPath, error);
        }

        try
        {
            File.Delete(sourcePath);
            return FileMoveResult.Moved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Both names point to the same file; the new name is ours, so removing it leaves the source as it was.
            _logger.LogWarning(
                exception,
                "Cannot remove {Source} after linking it to {Target}",
                sourcePath,
                targetPath);
            TryDelete(targetPath);
            return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
        }
    }

    private FileMoveResult CopyAndDelete(string sourcePath, string targetPath)
    {
        return CopyExclusive(sourcePath, targetPath, deleteSource: true);
    }

    /// <summary>
    /// Copies into a target created with <see cref="FileMode.CreateNew"/> (<c>O_EXCL</c>, <c>CREATE_NEW</c>), so an
    /// existing target is never touched. Once the target was created by this call, any failure removes it again.
    /// </summary>
    private FileMoveResult CopyExclusive(string sourcePath, string targetPath, bool deleteSource)
    {
        FileStream source;
        try
        {
            source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1,
                FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return FileMoveResult.SourceMissing;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Cannot read {Source}", sourcePath);
            return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
        }

        using (source)
        {
            FileStream target;
            try
            {
                target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1);
            }
            catch (IOException) when (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                return FileMoveResult.TargetExists;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Cannot create {Target}", targetPath);
                return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
            }

            try
            {
                using (target)
                {
                    byte[] buffer = new byte[CopyBufferSize];
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        target.Write(buffer, 0, read);
                    }

                    target.Flush(flushToDisk: true);
                    if (target.Length != source.Length)
                    {
                        throw new IOException("The copy is not as long as the source.");
                    }
                }

                File.SetLastWriteTimeUtc(targetPath, File.GetLastWriteTimeUtc(sourcePath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Cannot copy {Source} to {Target}", sourcePath, targetPath);
                TryDelete(targetPath);
                return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
            }
        }

        if (!deleteSource)
        {
            return FileMoveResult.Moved;
        }

        try
        {
            BeforeSourceDelete?.Invoke(targetPath);
            File.Delete(sourcePath);
            return FileMoveResult.Moved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                exception,
                "Cannot remove {Source} after copying it to {Target}",
                sourcePath,
                targetPath);
            TryDelete(targetPath);
            return new FileMoveResult(FileMoveStatus.Failed, exception.HResult);
        }
    }

    private FileMoveResult Failed(string sourcePath, string targetPath, int error)
    {
        _logger.LogWarning(
            "Cannot move {Source} to {Target}: error number {ErrorNumber}",
            sourcePath,
            targetPath,
            error);
        return new FileMoveResult(FileMoveStatus.Failed, error);
    }

    /// <summary>
    /// Removes a target this mover created itself.
    /// </summary>
    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Cannot remove the unfinished copy {Path}", path);
        }
    }
}
