namespace WaveMP.Service.Dtos;

/// <summary>
/// The fields of a match the admin applies to an album.
/// </summary>
public sealed class ApplyAlbumOnlineMetadataDto
{
    /// <summary>
    /// The album tags to write into every song of the album that can be written: missing members stay unchanged.
    /// </summary>
    public UpdateAlbumTagsDto? Tags { get; init; }

    /// <summary>
    /// The release whose front cover becomes the cover of the album; null for none.
    /// </summary>
    public Guid? CoverReleaseId { get; init; }
}
