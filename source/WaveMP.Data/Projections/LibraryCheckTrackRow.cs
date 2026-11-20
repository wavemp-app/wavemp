namespace WaveMP.Data.Projections;

/// <summary>
/// What the library check needs to know of a track.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the artist; <c>null</c> if unknown.</param>
/// <param name="AlbumId">Id of the album; <c>null</c> for tracks without album ("Unknown album").</param>
/// <param name="Duration">Duration of the track; <c>null</c> if unknown.</param>
/// <param name="HasAudio">Whether an audio file exists.</param>
/// <param name="HasVideo">Whether a video file exists.</param>
/// <param name="HasCover">Whether a cover exists: the track's own or its album's, also online covers of
/// both.</param>
/// <param name="MissingTags">Which of title, artist and album the tags of its file lack: of the audio file the track
/// plays, for tracks with a video only of the video; all three without such a file.</param>
public sealed record LibraryCheckTrackRow(
    Guid TrackId,
    string Title,
    Guid? ArtistId,
    Guid? AlbumId,
    TimeSpan? Duration,
    bool HasAudio,
    bool HasVideo,
    bool HasCover,
    IReadOnlyList<MetadataTag> MissingTags);
