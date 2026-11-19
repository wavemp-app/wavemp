namespace WaveMP.Service.Dtos;

/// <summary>
/// A file with a problem found by a scan, or a media source the scan skipped (with an empty path).
/// </summary>
public sealed class ScanIssueDto
{
    /// <summary>
    /// Path of the file relative to the folder of its media source; empty for a problem of the source itself.
    /// </summary>
    public required string Path { get; init; }

    public required ScanIssueCodeDto Code { get; init; }

    /// <summary>
    /// Id of the media source; null in results of scans from before there were several sources.
    /// </summary>
    public Guid? SourceId { get; init; }

    /// <summary>
    /// Name of the media source at the time of the scan.
    /// </summary>
    public string? SourceName { get; init; }
}
