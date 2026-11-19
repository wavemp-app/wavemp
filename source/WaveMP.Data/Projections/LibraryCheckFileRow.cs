using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A file that the scan could not read completely.
/// </summary>
/// <param name="MediaFileId">Id of the file.</param>
/// <param name="TrackId">Id of its track; <c>null</c> for files without a track, e.g. pictures.</param>
/// <param name="Issue">What cannot be read.</param>
public sealed record LibraryCheckFileRow(Guid MediaFileId, Guid? TrackId, MediaFileIssue Issue);
