namespace WaveMP.Service.Dtos;

/// <summary>
/// The fields of a match the admin applies to a song.
/// </summary>
public sealed class ApplyTrackOnlineMetadataDto
{
    /// <summary>
    /// The tags to write, like <c>PATCH /api/v1/tracks/{id}/metadata</c>: missing members stay unchanged. Written into
    /// the audio file only, after its current tags are saved as backup.
    /// </summary>
    public UpdateTrackMetadataDto? Tags { get; init; }

    /// <summary>
    /// The release whose front cover becomes the cover, following the cover rules; null for none.
    /// </summary>
    public Guid? CoverReleaseId { get; init; }
}
