using WaveMP.Core.Scanning.Plans;

namespace WaveMP.Core.Scanning.Scans;

/// <summary>
/// Scans the media sources and updates the library.
/// </summary>
public interface ILibraryScanner
{
    /// <summary>
    /// Scans the folders of the scope; the progress is reported for the run. A song whose folder was renamed or moved
    /// or whose files were renamed within the folders of the scan keeps its id and all its data.
    /// </summary>
    /// <exception cref="WaveMP.Core.Jobs.Runs.JobFailedException">
    /// There is no active source, the folders of all sources of the scope are missing, unreadable or unexpectedly
    /// empty, or ffprobe is not available.
    /// </exception>
    Task<ScanResult> ScanAsync(ScanScope scope, Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one folder of a source (without its subfolders) again at once, with the logic of the scan but without a
    /// job run and without progress: new, changed and removed files, and the files of <paramref name="changedFiles"/>
    /// even if their size and change time are the same; a song whose files were renamed in the folder keeps its id and
    /// all its data. Then removes artists and albums without tracks and updates the album covers. Waits while a scan
    /// writes the library. Changes nothing if the source, its folder or the folder is missing; removing whole folders
    /// is left to the scan. Returns whether anything changed.
    /// </summary>
    /// <param name="sourceId">Id of the media source of the folder.</param>
    /// <param name="directoryPath">Path of the folder relative to the folder of the source; empty for the folder of
    /// the source itself.</param>
    /// <param name="changedFiles">Relative paths of files of the folder that changed for sure.</param>
    /// <param name="cancellationToken">Aborts reading the folder.</param>
    /// <exception cref="WaveMP.Core.Metadata.Probes.ProbeUnavailableException">ffprobe cannot be started.</exception>
    Task<bool> RefreshFolderAsync(
        Guid sourceId,
        string directoryPath,
        IReadOnlyCollection<string> changedFiles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings the library in line with folders whose files WaveMP moved or renamed after it stored their new paths
    /// (the organization of the media files): each folder is read like <see cref="RefreshFolderAsync"/>; a folder
    /// without new, changed or removed files has its songs formed again from the stored files, so albums known by
    /// their folder and metadata taken from the path follow the new place. Then removes artists and albums without
    /// tracks and updates album covers, artist pictures and compilations once for all folders. Missing folders are
    /// skipped. Holds the <see cref="Writers.LibraryWriteLock"/> for each folder, not the
    /// <see cref="Writers.LibraryMaintenanceLock"/>. Returns the number of folders read.
    /// </summary>
    /// <param name="folders">The folders with the files that changed for sure, e.g. after their tags were
    /// written.</param>
    /// <param name="cancellationToken">Aborts reading the folders.</param>
    /// <exception cref="WaveMP.Core.Metadata.Probes.ProbeUnavailableException">ffprobe cannot be started.</exception>
    Task<int> RefreshFoldersAsync(
        IReadOnlyCollection<FolderRefresh> folders,
        CancellationToken cancellationToken = default);
}
