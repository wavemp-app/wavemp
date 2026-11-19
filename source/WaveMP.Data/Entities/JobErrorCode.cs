namespace WaveMP.Data.Entities;

/// <summary>
/// Machine-readable reason why a job run failed. Stored as its name; clients translate it.
/// </summary>
public enum JobErrorCode
{
    /// <summary>
    /// An unexpected error; details are in the log.
    /// </summary>
    Failed,

    /// <summary>
    /// The service stopped while the run was queued or running.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The media folder does not exist (the folder of the only media source that was scanned).
    /// </summary>
    MediaFolderNotFound,

    /// <summary>
    /// The media folder cannot be read.
    /// </summary>
    MediaFolderUnreadable,

    /// <summary>
    /// The media folder is empty although the library has files, e.g. because it is not mounted. Nothing was removed.
    /// </summary>
    MediaFolderEmpty,

    /// <summary>
    /// ffprobe cannot be started.
    /// </summary>
    ProbeUnavailable,

    /// <summary>
    /// There is no active media source to scan.
    /// </summary>
    NoMediaSources,

    /// <summary>
    /// ffmpeg cannot be started, e.g. to scale covers.
    /// </summary>
    FfmpegUnavailable,

    /// <summary>
    /// The cache folder under the data folder cannot be written.
    /// </summary>
    CacheFolderUnwritable,

    /// <summary>
    /// The backup folder under the data folder cannot be written.
    /// </summary>
    BackupFolderUnwritable,

    /// <summary>
    /// pg_dump cannot be started, so the database cannot be exported.
    /// </summary>
    PgDumpUnavailable,

    /// <summary>
    /// pg_dump failed or took too long; details are in the log.
    /// </summary>
    BackupFailed,

    /// <summary>
    /// The download of lyrics is switched off in the library settings.
    /// </summary>
    LyricsDownloadDisabled,

    /// <summary>
    /// The lyrics service cannot be reached, e.g. without an internet connection, or keeps refusing requests.
    /// </summary>
    LyricsServiceUnavailable,
}
