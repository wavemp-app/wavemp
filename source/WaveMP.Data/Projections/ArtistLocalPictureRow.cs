namespace WaveMP.Data.Projections;

/// <summary>
/// The picture file in the folder of an artist.
/// </summary>
/// <param name="MediaFileId">Id of the media file.</param>
/// <param name="SourceId">Id of the media source of the file.</param>
/// <param name="RelativePath">Path of the file relative to the folder of its source.</param>
/// <param name="Size">Size of the file in bytes.</param>
/// <param name="ModifiedAt">Time of the last change of the file.</param>
public sealed record ArtistLocalPictureRow(
    Guid MediaFileId,
    Guid SourceId,
    string RelativePath,
    long Size,
    DateTimeOffset ModifiedAt);
