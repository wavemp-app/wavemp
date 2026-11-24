namespace WaveMP.Core.Scanning.Scans;

/// <summary>
/// A folder that <see cref="ILibraryScanner.RefreshFoldersAsync"/> reads again.
/// </summary>
/// <param name="SourceId">Id of the media source of the folder.</param>
/// <param name="DirectoryPath">Path of the folder relative to the folder of the source; empty for the folder of the
/// source itself.</param>
/// <param name="ChangedFiles">Relative paths of files of the folder that changed for sure.</param>
public sealed record FolderRefresh(Guid SourceId, string DirectoryPath, IReadOnlyCollection<string> ChangedFiles);
