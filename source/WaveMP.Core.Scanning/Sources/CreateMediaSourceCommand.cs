namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// A new media source.
/// </summary>
/// <param name="Name">Name, unique without regard to case.</param>
/// <param name="Path">Absolute path of the folder in the container.</param>
/// <param name="IsActive">Whether the source is scanned and watched; active if <c>null</c>.</param>
public sealed record CreateMediaSourceCommand(string Name, string Path, bool? IsActive = null);
