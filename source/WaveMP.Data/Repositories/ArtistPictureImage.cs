namespace WaveMP.Data.Repositories;

/// <summary>
/// A picture that may be the picture of the artist whose folder it lies in.
/// </summary>
public sealed record ArtistPictureImage(
    Guid MediaFileId,
    Guid SourceId,
    string RelativePath,
    string DirectoryPath,
    string NormalizedBaseName,
    long Size,
    int? Width,
    int? Height);
