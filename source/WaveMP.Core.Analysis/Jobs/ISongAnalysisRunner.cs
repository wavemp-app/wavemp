namespace WaveMP.Core.Analysis.Jobs;

/// <summary>
/// One run of the song analysis over the library.
/// </summary>
public interface ISongAnalysisRunner
{
    /// <summary>
    /// Analyzes all tracks that wait for an analysis.
    /// </summary>
    Task<SongAnalysisJobResult> RunAsync(CancellationToken cancellationToken = default);
}
