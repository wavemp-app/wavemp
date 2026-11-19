namespace WaveMP.Core.Scanning.Scans;

/// <summary>
/// Problem with a file or a media source found by a scan.
/// </summary>
public enum ScanIssueCode
{
    /// <summary>
    /// The file cannot be read at all.
    /// </summary>
    FileUnreadable,

    /// <summary>
    /// The tags of the file cannot be read.
    /// </summary>
    TagsUnreadable,

    /// <summary>
    /// ffprobe cannot read the file.
    /// </summary>
    ProbeFailed,

    /// <summary>
    /// The path of the file is too long; the file was skipped.
    /// </summary>
    PathTooLong,

    /// <summary>
    /// The folder of the source does not exist; the source was skipped and its library left unchanged.
    /// </summary>
    SourceNotFound,

    /// <summary>
    /// The folder of the source cannot be read; the source was skipped and its library left unchanged.
    /// </summary>
    SourceUnreadable,

    /// <summary>
    /// The folder of the source has no media files although the library has files of it, e.g. because it is not
    /// mounted; the source was skipped and nothing of it was removed.
    /// </summary>
    SourceEmpty,
}
