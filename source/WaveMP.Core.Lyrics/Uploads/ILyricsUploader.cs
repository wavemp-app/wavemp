namespace WaveMP.Core.Lyrics.Uploads;

/// <summary>
/// Lyrics admins upload for a track, as LRC text (with times) or plain text, saved like downloaded lyrics: as
/// <c>.lrc</c> file with the base name of the track, replacing an existing lyrics file, or in the database if the
/// folder is not writable. On request they are published to LRCLIB, while the download is switched on in the library
/// settings.
/// </summary>
public interface ILyricsUploader
{
    /// <summary>
    /// Longest uploaded text in characters.
    /// </summary>
    const int MaxLength = 256 * 1024;

    /// <summary>
    /// Saves the lyrics and publishes them to LRCLIB if asked to. Publishing happens after saving; if it fails, the
    /// lyrics stay saved.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="text">The lyrics in the LRC format or as plain lines.</param>
    /// <param name="publish">Whether the lyrics are also published to LRCLIB.</param>
    /// <param name="cancellationToken">Aborts the upload.</param>
    Task<LyricsUploadResult> UploadAsync(
        Guid trackId,
        string text,
        bool publish,
        CancellationToken cancellationToken = default);
}
