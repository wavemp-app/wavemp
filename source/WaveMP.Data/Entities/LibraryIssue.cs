using WaveMP.Data.Projections;

namespace WaveMP.Data.Entities;

/// <summary>
/// A problem of the library found by the library check: a track without cover or tags, a possible duplicate, an
/// unreadable file and so on. Every run of the check replaces the problems; a problem that is still found keeps its
/// id and the time it was found first. Removed with its track or file.
/// </summary>
public class LibraryIssue
{
    public Guid Id { get; set; }

    public required LibraryIssueType Type { get; set; }

    /// <summary>
    /// The track with the problem; <c>null</c> for an unreadable file that belongs to no track.
    /// </summary>
    public Guid? TrackId { get; set; }

    public Track? Track { get; set; }

    /// <summary>
    /// The file with the problem, for <see cref="LibraryIssueType.UnreadableFile"/> only.
    /// </summary>
    public Guid? MediaFileId { get; set; }

    public MediaFile? MediaFile { get; set; }

    /// <summary>
    /// What cannot be read of the file, for <see cref="LibraryIssueType.UnreadableFile"/> only. Stored as its name.
    /// </summary>
    public MediaFileIssue? FileIssue { get; set; }

    /// <summary>
    /// The tags the file of the track lacks, for <see cref="LibraryIssueType.MissingTags"/> only; empty otherwise.
    /// </summary>
    public List<MetadataTag> MissingTags { get; set; } = [];

    /// <summary>
    /// Another track of the group of possible duplicates, for <see cref="LibraryIssueType.PossibleDuplicate"/> only;
    /// <c>null</c> otherwise.
    /// </summary>
    public Guid? RelatedTrackId { get; set; }

    public Track? RelatedTrack { get; set; }

    /// <summary>
    /// When the check found the problem first; kept while later runs find it again.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; set; }
}
