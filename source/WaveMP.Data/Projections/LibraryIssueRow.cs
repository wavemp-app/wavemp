using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A problem of the library with its track, file and details.
/// </summary>
/// <param name="Id">Id of the problem.</param>
/// <param name="Type">Kind of the problem.</param>
/// <param name="DetectedAt">When the check found the problem first.</param>
/// <param name="Track">The track with the problem; <c>null</c> for an unreadable file without a track.</param>
/// <param name="File">The unreadable file; <c>null</c> for other problems.</param>
/// <param name="FileIssue">What cannot be read of the file; <c>null</c> for other problems.</param>
/// <param name="MissingTags">The tags the file of the track lacks; empty for other problems.</param>
/// <param name="RelatedTrack">Another track of the group of possible duplicates; <c>null</c> for other problems.
/// </param>
public sealed record LibraryIssueRow(
    Guid Id,
    LibraryIssueType Type,
    DateTimeOffset DetectedAt,
    LibraryIssueTrackRow? Track,
    LibraryIssueFileRow? File,
    MediaFileIssue? FileIssue,
    IReadOnlyList<MetadataTag> MissingTags,
    LibraryIssueTrackRow? RelatedTrack);
