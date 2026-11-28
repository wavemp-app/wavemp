namespace WaveMP.Service.Dtos;

/// <summary>
/// A track in the review inbox: what identifies it, the fields its tags lack and its review status.
/// </summary>
public sealed class TrackReviewDto
{
    public required Guid TrackId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Artist of the track; null if unknown.
    /// </summary>
    public ArtistReferenceDto? Artist { get; init; }

    /// <summary>
    /// Album of the track; null for tracks without album.
    /// </summary>
    public AlbumReferenceDto? Album { get; init; }

    /// <summary>
    /// Folder of the track relative to the media folder with / as separator; empty for the media folder itself.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// File name of the track without extension, shared by all its files.
    /// </summary>
    public required string BaseName { get; init; }

    public required bool HasAudio { get; init; }

    public required bool HasVideo { get; init; }

    /// <summary>
    /// Whether GetTrackCover returns a picture.
    /// </summary>
    public required bool HasCover { get; init; }

    /// <summary>
    /// When the track was found for the first time (date added).
    /// </summary>
    public required DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>
    /// The checked fields the tags lack: of the audio file the track plays, for tracks with a video only of the video.
    /// Empty if the tags are complete.
    /// </summary>
    public required IReadOnlyList<MetadataTagDto> MissingTags { get; init; }

    /// <summary>
    /// Why the song segment of the audio file or the video needs a look (see GetTrackSongSegments); null if it does
    /// not.
    /// </summary>
    public SongSegmentNoticeDto? SongSegmentNotice { get; init; }

    public required TrackReviewStatusDto Status { get; init; }

    /// <summary>
    /// When the track was marked as reviewed; null while unreviewed.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>
    /// The profile that marked the track as reviewed; null while unreviewed or after the profile was deleted.
    /// </summary>
    public ProfileReferenceDto? ReviewedBy { get; init; }
}
