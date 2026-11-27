namespace WaveMP.Service.Dtos;

/// <summary>
/// The result of uploading lyrics: the lyrics the track has now and what publishing them to LRCLIB did.
/// </summary>
public sealed class LyricsUploadDto
{
    /// <summary>
    /// The lyrics of the track after the upload.
    /// </summary>
    public required LyricsDto Lyrics { get; init; }

    /// <summary>
    /// What publishing to LRCLIB did; the lyrics stay saved if it failed.
    /// </summary>
    public required LyricsPublicationDto Publication { get; init; }
}
