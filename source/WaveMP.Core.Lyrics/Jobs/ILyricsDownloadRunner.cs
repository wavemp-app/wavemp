namespace WaveMP.Core.Lyrics.Jobs;

/// <summary>
/// One run of the download job.
/// </summary>
public interface ILyricsDownloadRunner
{
    /// <summary>
    /// Looks up the lyrics of all tracks without lyrics file that were never looked up, or whose lookup found no lyrics
    /// a while ago.
    /// </summary>
    /// <exception cref="WaveMP.Core.Jobs.Runs.JobFailedException">The download is switched off, or LRCLIB cannot be
    /// reached or keeps refusing requests.</exception>
    Task<LyricsDownloadJobResult> RunAsync(CancellationToken cancellationToken = default);
}
