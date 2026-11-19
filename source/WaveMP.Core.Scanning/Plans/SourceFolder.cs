namespace WaveMP.Core.Scanning.Plans;

/// <summary>
/// A folder of a media source.
/// </summary>
/// <param name="SourceId">Id of the source.</param>
/// <param name="Path">Path relative to the folder of the source with <c>/</c>; empty for the folder of the source
/// itself.</param>
public sealed record SourceFolder(Guid SourceId, string Path);
