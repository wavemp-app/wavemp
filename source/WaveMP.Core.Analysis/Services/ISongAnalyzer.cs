namespace WaveMP.Core.Analysis.Services;

/// <summary>
/// Analyzes the primary audio file and the primary video of one track: where the real song lies in each of them and
/// how loud they are. Also looks up the lyrics of the track in the order the detection needs them.
/// </summary>
public interface ISongAnalyzer
{
    /// <summary>
    /// Analyzes the files of the track and stores the results.
    /// <list type="bullet">
    /// <item>With an audio file and a video with sound, the audio file is aligned with the video first; after that,
    /// the lyrics are looked up with the duration of the real song.</item>
    /// <item>With only one file with sound, its lyrics are looked up first, without relying on the duration; then the
    /// song is found by aligning them, otherwise by the structure of the file.</item>
    /// </list>
    /// Corrections of the admin are never changed.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="cancellationToken">Stops the analysis; nothing is stored for the track then.</param>
    Task<SongAnalysisResult> AnalyzeAsync(Guid trackId, CancellationToken cancellationToken = default);
}
