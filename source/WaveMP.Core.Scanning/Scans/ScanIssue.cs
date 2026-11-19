namespace WaveMP.Core.Scanning.Scans;

/// <summary>
/// A file with a problem, by its path relative to the folder of its media source, or a source that could not be
/// scanned (with an empty path).
/// </summary>
/// <param name="Path">Path of the file relative to the folder of the source; empty for a problem of the source.</param>
/// <param name="Code">The problem.</param>
/// <param name="SourceId">Id of the source; <c>null</c> in results of scans before there were several sources.</param>
/// <param name="SourceName">Name of the source at the time of the scan.</param>
public sealed record ScanIssue(string Path, ScanIssueCode Code, Guid? SourceId = null, string? SourceName = null);
