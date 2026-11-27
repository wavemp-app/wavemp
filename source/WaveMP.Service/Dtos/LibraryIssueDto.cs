namespace WaveMP.Service.Dtos;

/// <summary>
/// A problem of the library found by the library check, with its track, file and details. Clients show their own
/// text for the type and the codes of the details.
/// </summary>
public sealed class LibraryIssueDto
{
    /// <summary>
    /// Id of the problem; stays the same as long as later runs of the check find it again.
    /// </summary>
    public required Guid Id { get; init; }

    public required LibraryIssueTypeDto Type { get; init; }

    public required LibraryIssueSeverityDto Severity { get; init; }

    /// <summary>
    /// When the check found the problem first.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// The track with the problem; null for an unreadable file that belongs to no track (e.g. a picture).
    /// </summary>
    public LibraryIssueTrackDto? Track { get; init; }

    /// <summary>
    /// The unreadable file, for UnreadableFile only.
    /// </summary>
    public LibraryIssueFileDto? File { get; init; }

    /// <summary>
    /// What cannot be read of the file, for UnreadableFile only; the codes of the scan issues.
    /// </summary>
    public ScanIssueCodeDto? FileIssue { get; init; }

    /// <summary>
    /// The tags the file of the track lacks (Title, Artist, Album), for MissingTags only; empty otherwise.
    /// </summary>
    public required IReadOnlyList<MetadataTagDto> MissingTags { get; init; }

    /// <summary>
    /// Another track of the group of possible duplicates, for PossibleDuplicate only.
    /// </summary>
    public LibraryIssueTrackDto? RelatedTrack { get; init; }

    /// <summary>
    /// The artists of the track split as suggested, for PossibleMultipleArtists only: each name that may name several
    /// artists is replaced by its parts. Confirming writes them as the artists of the track; keeping writes the
    /// current artists as they are. Both mark the artists as confirmed, and the hint is gone after the next check.
    /// </summary>
    public IReadOnlyList<ArtistNameDto>? SuggestedArtists { get; init; }
}
