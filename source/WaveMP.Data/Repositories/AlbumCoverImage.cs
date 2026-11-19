namespace WaveMP.Data.Repositories;

/// <summary>
/// A picture that may be the cover of the albums in its folder.
/// </summary>
public sealed record AlbumCoverImage(
    Guid MediaFileId,
    Guid SourceId,
    string RelativePath,
    string DirectoryPath,
    string NormalizedBaseName,
    string Extension,
    long Size,
    int? Width,
    int? Height);
