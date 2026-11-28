namespace WaveMP.Data.Projections;

/// <summary>
/// Why the song segment of a track needs a look of the admin.
/// </summary>
public enum SongSegmentNotice
{
    /// <summary>
    /// The analysis found a segment but is not sure about it; the players play the whole file.
    /// </summary>
    LowConfidence,

    /// <summary>
    /// The audio file and the video are different recordings (e.g. a live version or a remix), so positions cannot be
    /// mapped between them.
    /// </summary>
    Mismatch,
}
