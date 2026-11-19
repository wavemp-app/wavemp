namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// Changes of a media source; <c>null</c> values stay unchanged.
/// </summary>
/// <param name="Name">New name.</param>
/// <param name="Path">New absolute path of the folder in the container.</param>
/// <param name="IsActive">Whether the source is scanned and watched.</param>
public sealed record UpdateMediaSourceCommand(string? Name = null, string? Path = null, bool? IsActive = null);
