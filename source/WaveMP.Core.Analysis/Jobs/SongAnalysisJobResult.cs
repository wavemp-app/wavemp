namespace WaveMP.Core.Analysis.Jobs;

/// <summary>
/// Result of a run of the song analysis, stored as JSON with the run.
/// </summary>
/// <param name="TracksAnalyzed">Tracks whose files were analyzed.</param>
/// <param name="NeedReview">Of these, tracks whose song segment needs a look of the admin.</param>
/// <param name="Failed">Tracks with a file whose sound could not be read.</param>
public sealed record SongAnalysisJobResult(int TracksAnalyzed, int NeedReview, int Failed);
