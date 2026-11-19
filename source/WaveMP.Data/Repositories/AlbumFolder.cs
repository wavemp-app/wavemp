namespace WaveMP.Data.Repositories;

/// <summary>
/// A folder of a source with tracks of an album.
/// </summary>
public sealed record AlbumFolder(Guid AlbumId, Guid SourceId, string DirectoryPath);
