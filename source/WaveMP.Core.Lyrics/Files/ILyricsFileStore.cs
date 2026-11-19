namespace WaveMP.Core.Lyrics.Files;

/// <summary>
/// Reads and creates lyrics files of the media sources.
/// </summary>
public interface ILyricsFileStore
{
    /// <summary>
    /// The text of a lyrics file, or <c>null</c> if it is missing, larger than allowed or cannot be read.
    /// </summary>
    /// <param name="sourceId">Id of the media source of the file.</param>
    /// <param name="relativePath">Path relative to the folder of the source with <c>/</c> as separator.</param>
    /// <param name="cancellationToken">Aborts reading.</param>
    Task<string?> ReadAsync(Guid sourceId, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a lyrics file with the text (UTF-8); an existing file is never replaced.
    /// </summary>
    /// <param name="sourceId">Id of the media source of the file.</param>
    /// <param name="relativePath">Path relative to the folder of the source with <c>/</c> as separator.</param>
    /// <param name="text">The lyrics.</param>
    /// <param name="cancellationToken">Aborts writing.</param>
    Task<LyricsFileWriteStatus> CreateAsync(
        Guid sourceId,
        string relativePath,
        string text,
        CancellationToken cancellationToken = default);
}
