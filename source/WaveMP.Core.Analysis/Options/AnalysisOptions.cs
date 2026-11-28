namespace WaveMP.Core.Analysis.Options;

/// <summary>
/// Technical settings of the song analysis (configuration section <c>Analysis</c>, e.g.
/// <c>Analysis__MaxDuration</c>).
/// </summary>
public sealed class AnalysisOptions
{
    public const string SectionName = "Analysis";

    /// <summary>
    /// Version of the analysis. Files analyzed by an older version are analyzed again; raised whenever the detection
    /// changes in a way that improves earlier results.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Only this much of the start of a file is analyzed; longer files (e.g. concerts) are analyzed up to here.
    /// </summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// How long the decoding of one file may take.
    /// </summary>
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// While transcodes run (someone plays a transcoded song or video), the analysis waits between two songs, checking
    /// again after this time ...
    /// </summary>
    public TimeSpan TranscodeWaitInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// ... but at most this long, then it goes on at its low priority.
    /// </summary>
    public TimeSpan MaxTranscodeWait { get; set; } = TimeSpan.FromMinutes(10);
}
