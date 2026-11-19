namespace WaveMP.Core.Jobs.Definitions;

/// <summary>
/// Stable ids of all background jobs. They name the jobs in the API and in the run history.
/// </summary>
public static class JobIds
{
    /// <summary>
    /// Scans the media folder and updates the library.
    /// </summary>
    public const string LibraryScan = "library-scan";

    /// <summary>
    /// Prepares the scaled covers after a scan and removes those of changed or removed pictures from the cache.
    /// </summary>
    public const string CoverCache = "cover-cache";

    /// <summary>
    /// Exports the database into the backup folder, every day and when an admin starts it.
    /// </summary>
    public const string DatabaseBackup = "database-backup";

    /// <summary>
    /// Downloads the lyrics of tracks without lyrics from the online lyrics database, after scans and when the
    /// download is switched on.
    /// </summary>
    public const string LyricsDownload = "lyrics-download";

    /// <summary>
    /// Checks the library for problems (missing covers and tags, duplicates, unreadable files, ...) after every scan
    /// and when an admin starts it.
    /// </summary>
    public const string LibraryCheck = "library-check";
}
