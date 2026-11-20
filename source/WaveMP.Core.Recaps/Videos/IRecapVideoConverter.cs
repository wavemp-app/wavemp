namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// Converts a video a browser recorded of a recap (WebM, Matroska or MP4) into MP4 with H.264 and AAC, the format
/// phones and messengers share without problems.
/// </summary>
public interface IRecapVideoConverter
{
    /// <summary>
    /// Writes the content into a temporary file in the data folder, converts it with ffmpeg and deletes the uploaded
    /// file again. The content may be at most <c>Recaps:MaxVideoBytes</c> long.
    /// </summary>
    Task<RecapVideoResult> ConvertAsync(Stream content, CancellationToken cancellationToken = default);
}
