using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// A problem found by a run of the library check. A problem is the same as a stored one if type, track and file are the
/// same; the details may change.
/// </summary>
/// <param name="Type">Kind of the problem.</param>
/// <param name="TrackId">The track with the problem; <c>null</c> for an unreadable file without a track.</param>
/// <param name="MediaFileId">The unreadable file; <c>null</c> for other problems.</param>
/// <param name="FileIssue">What cannot be read of the file; <c>null</c> for other problems.</param>
/// <param name="MissingTags">The tags the file of the track lacks; <c>null</c> or empty for other problems.</param>
/// <param name="RelatedTrackId">Another track of the group of possible duplicates; <c>null</c> for other problems.
/// </param>
public sealed record DetectedLibraryIssue(
    LibraryIssueType Type,
    Guid? TrackId,
    Guid? MediaFileId = null,
    MediaFileIssue? FileIssue = null,
    IReadOnlyList<MetadataTag>? MissingTags = null,
    Guid? RelatedTrackId = null);
