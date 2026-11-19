using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The file of a problem of the library.
/// </summary>
/// <param name="Id">Id of the file.</param>
/// <param name="SourceId">Id of the media source of the file.</param>
/// <param name="SourceName">Name of the media source.</param>
/// <param name="RelativePath">Path of the file relative to the folder of its source.</param>
/// <param name="Kind">Kind of the file.</param>
public sealed record LibraryIssueFileRow(
    Guid Id,
    Guid SourceId,
    string SourceName,
    string RelativePath,
    MediaFileKind Kind);
