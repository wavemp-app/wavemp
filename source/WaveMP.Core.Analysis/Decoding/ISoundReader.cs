namespace WaveMP.Core.Analysis.Decoding;

/// <summary>
/// Reads the sound of a media file (an audio file or the first audio track of a video) for the song analysis.
/// </summary>
public interface ISoundReader
{
    /// <summary>
    /// Decodes the sound with ffmpeg at a low priority, builds its features and measures its loudness.
    /// </summary>
    /// <param name="fullPath">Absolute path of the file.</param>
    /// <param name="cancellationToken">Stops the decoding.</param>
    Task<SoundReading> ReadAsync(string fullPath, CancellationToken cancellationToken = default);
}
