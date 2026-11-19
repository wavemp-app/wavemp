namespace WaveMP.Service.Dtos;

/// <summary>
/// A media source: a folder of the container whose media files the library contains.
/// </summary>
public sealed class MediaSourceDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Name of the source, unique without regard to case.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Absolute path of the folder in the container.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether the folder comes from the environment variable MEDIA_PATH (the first source as long as no path of its
    /// own was set).
    /// </summary>
    public required bool FollowsMediaPath { get; init; }

    /// <summary>
    /// Whether the source is scanned and watched. The tracks of a deactivated source stay in the library.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Whether the folder exists in the container.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Whether the file watcher watches the folder right now.
    /// </summary>
    public required bool IsWatched { get; init; }

    /// <summary>
    /// Number of tracks of the source in the library.
    /// </summary>
    public required int TrackCount { get; init; }

    /// <summary>
    /// Number of media files of the source in the library.
    /// </summary>
    public required int FileCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
