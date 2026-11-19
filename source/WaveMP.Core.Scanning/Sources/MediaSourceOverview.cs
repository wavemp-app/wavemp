namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// A media source with its state.
/// </summary>
/// <param name="Id">Id of the source.</param>
/// <param name="Name">Name of the source.</param>
/// <param name="Path">Absolute path of the folder in the container.</param>
/// <param name="FollowsMediaPath">Whether the folder comes from <c>MEDIA_PATH</c> (the first source as long as the
/// admin set no path of its own).</param>
/// <param name="IsActive">Whether the source is scanned and watched.</param>
/// <param name="IsAvailable">Whether the folder exists.</param>
/// <param name="IsWatched">Whether the file watcher watches the folder right now.</param>
/// <param name="TrackCount">Number of tracks of the source in the library.</param>
/// <param name="FileCount">Number of media files of the source in the library.</param>
/// <param name="CreatedAt">When the source was added.</param>
/// <param name="UpdatedAt">When the source was changed last.</param>
public sealed record MediaSourceOverview(
    Guid Id,
    string Name,
    string Path,
    bool FollowsMediaPath,
    bool IsActive,
    bool IsAvailable,
    bool IsWatched,
    int TrackCount,
    int FileCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
