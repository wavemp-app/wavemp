namespace WaveMP.Service.Dtos;

/// <summary>
/// The song segment of a file of a track with the result of the analysis and the corrections of the admin.
/// </summary>
public sealed class FileSongSegmentDto
{
    /// <summary>
    /// Id of the file.
    /// </summary>
    public required Guid FileId { get; init; }

    /// <summary>
    /// Duration of the file in milliseconds, if known.
    /// </summary>
    public long? DurationMilliseconds { get; init; }

    /// <summary>
    /// Whether the analysis could read the file; null while it was never analyzed.
    /// </summary>
    public SongAnalysisStatusDto? Status { get; init; }

    /// <summary>
    /// Whether the file waits for a (new) analysis.
    /// </summary>
    public required bool Pending { get; init; }

    /// <summary>
    /// When the file was analyzed; null while it was never analyzed.
    /// </summary>
    public DateTimeOffset? AnalyzedAt { get; init; }

    /// <summary>
    /// Integrated loudness of the sound in LUFS (EBU R 128), if measured. Clients play the audio file and the sound of
    /// the video at the same loudness with it.
    /// </summary>
    public double? LoudnessLufs { get; init; }

    /// <summary>
    /// The segment the analysis found, without the corrections of the admin.
    /// </summary>
    public required SongSegmentDto Detected { get; init; }

    /// <summary>
    /// Start of the song as the admin corrected it, in milliseconds of the file; null without correction.
    /// </summary>
    public int? ManualStartMilliseconds { get; init; }

    /// <summary>
    /// End of the song as the admin corrected it, in milliseconds of the file; null without correction.
    /// </summary>
    public int? ManualEndMilliseconds { get; init; }

    /// <summary>
    /// The segment the players use.
    /// </summary>
    public required SongSegmentDto Effective { get; init; }
}
