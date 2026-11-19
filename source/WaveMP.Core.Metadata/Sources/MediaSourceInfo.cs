namespace WaveMP.Core.Metadata.Sources;

/// <summary>
/// A media source as the scan, the file watcher and the access to files need it.
/// </summary>
/// <param name="Id">Id of the source.</param>
/// <param name="Name">Name of the source.</param>
/// <param name="RootPath">Absolute path of the folder of the source: its own path, or <c>MEDIA_PATH</c> for the first
/// source as long as it has none.</param>
/// <param name="IsActive">Whether the source is scanned and watched.</param>
/// <param name="FollowsMediaPath">Whether the folder comes from <c>MEDIA_PATH</c>.</param>
public sealed record MediaSourceInfo(Guid Id, string Name, string RootPath, bool IsActive, bool FollowsMediaPath);
