namespace WaveMP.Data.Projections;

/// <summary>
/// A track in the review inbox: what identifies it, the fields its tags lack and its review status.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the artist of the track.</param>
/// <param name="ArtistName">Name of the artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the album artist.</param>
/// <param name="AlbumArtistName">Name of the album artist.</param>
/// <param name="AlbumIsCompilation">Whether the album is a compilation.</param>
/// <param name="DirectoryPath">Folder of the track relative to the media folder.</param>
/// <param name="BaseName">File name of the track without extension.</param>
/// <param name="HasAudio">Whether an audio file exists.</param>
/// <param name="HasVideo">Whether a video file exists.</param>
/// <param name="HasCover">Whether a cover exists: the track's own or its album's, also online covers of
/// both.</param>
/// <param name="FirstSeenAt">When the track was found for the first time.</param>
/// <param name="MissingTags">The checked fields the tags of its file lack: of the audio file the track plays, for
/// tracks with a video only of the video.</param>
/// <param name="Review">The review status.</param>
public sealed record TrackReviewRow(
    Guid TrackId,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    Guid? AlbumArtistId,
    string? AlbumArtistName,
    bool AlbumIsCompilation,
    string DirectoryPath,
    string BaseName,
    bool HasAudio,
    bool HasVideo,
    bool HasCover,
    DateTimeOffset FirstSeenAt,
    IReadOnlyList<MetadataTag> MissingTags,
    TrackReviewStateRow Review);
