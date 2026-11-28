using WaveMP.Data.Entities;

namespace WaveMP.Data.Segments;

/// <summary>
/// Where the real song lies in a media file, as the players use it: the corrections of the admin if there are any,
/// otherwise what the song analysis found. <see cref="Pieces"/> map positions of the file to positions of the song
/// (the time of the reference recording that synchronized lyrics follow); without pieces, the whole file is the song.
/// </summary>
/// <param name="Pieces">The parts of the file that belong to the song, in the order of the file; empty if the whole
/// file is the song.</param>
/// <param name="Method">How the segment was found; <see cref="SongSegmentMethod.WholeFile"/> also for files that were
/// not analyzed yet.</param>
/// <param name="Confidence">How sure the analysis is, between 0 and 1; 1 for corrections of the admin, <c>null</c> for
/// <see cref="SongSegmentMethod.WholeFile"/>.</param>
/// <param name="Manual">Whether the admin corrected start or end.</param>
/// <param name="Mismatch">Whether the file did not match the other recording of the track.</param>
public sealed record SongSegment(
    IReadOnlyList<SongPiece> Pieces,
    SongSegmentMethod Method,
    double? Confidence,
    bool Manual,
    bool Mismatch)
{
    /// <summary>
    /// From this confidence on, the players use a found segment: they skip intro and outro, map positions between the
    /// audio file and the video and shift the lyrics. Below it, the admin gets a hint and the whole file counts.
    /// </summary>
    public const double ConfidentFrom = 0.6;

    /// <summary>
    /// Longest file a segment covers when the duration of the file is unknown (24 hours).
    /// </summary>
    private const int UnknownDurationMilliseconds = 24 * 60 * 60 * 1000;

    /// <summary>
    /// The whole file is the song.
    /// </summary>
    public static SongSegment WholeFile { get; } = new([], SongSegmentMethod.WholeFile, null, false, false);

    /// <summary>
    /// Whether the players use the segment: a correction of the admin, or a found segment with a confidence of at least
    /// <see cref="ConfidentFrom"/>. A whole file is always used as it is.
    /// </summary>
    public bool IsConfident => Manual
        || Method == SongSegmentMethod.WholeFile
        || Confidence is { } confidence && confidence >= ConfidentFrom;

    /// <summary>
    /// Whether the admin gets a hint: a found segment the analysis is not sure about, or a file that did not match the
    /// other recording of the track.
    /// </summary>
    public bool NeedsReview => !Manual && (Mismatch || !IsConfident);

    /// <summary>
    /// Position of the file where the song starts; may be negative if the file starts after the beginning of the
    /// song (only from corrections of the admin). 0 if the whole file is the song.
    /// </summary>
    public int StartMilliseconds => Pieces.Count == 0
        ? 0
        : Pieces[0].FileStartMilliseconds - Pieces[0].SongStartMilliseconds;

    /// <summary>
    /// Position of the file where the song ends; <c>null</c> if the whole file is the song.
    /// </summary>
    public int? EndMilliseconds => Pieces.Count == 0
        ? null
        : Pieces[^1].FileStartMilliseconds + Pieces[^1].DurationMilliseconds;

    /// <summary>
    /// Length of the song in this file (from its start in the song to its end, including parts the file cut out);
    /// <c>null</c> if the whole file is the song.
    /// </summary>
    public int? SongLengthMilliseconds => Pieces.Count == 0
        ? null
        : Pieces[^1].SongStartMilliseconds + Pieces[^1].DurationMilliseconds;

    /// <summary>
    /// The segment of a file: from the corrections of the admin if start or end is set (one piece from start to end;
    /// a value not set comes from the analysis or the file), otherwise from the analysis.
    /// </summary>
    /// <param name="analysis">The analysis of the file; <c>null</c> if it was not analyzed yet.</param>
    /// <param name="manualStart">Start set by the admin, in milliseconds of the file.</param>
    /// <param name="manualEnd">End set by the admin, in milliseconds of the file.</param>
    /// <param name="duration">Duration of the file, if known.</param>
    public static SongSegment Of(
        MediaFileAnalysis? analysis,
        int? manualStart,
        int? manualEnd,
        TimeSpan? duration)
    {
        bool mismatch = analysis?.Mismatch ?? false;
        SongSegment detected = analysis is { Pieces.Count: > 0 }
            ? new SongSegment([.. analysis.Pieces], analysis.Method, analysis.Confidence, false, mismatch)
            : new SongSegment([], SongSegmentMethod.WholeFile, null, false, mismatch);
        if (manualStart is null && manualEnd is null)
        {
            return detected;
        }

        int fileEnd = duration is { } known && known > TimeSpan.Zero
            ? (int)Math.Min(int.MaxValue, Math.Round(known.TotalMilliseconds))
            : UnknownDurationMilliseconds;
        int start = manualStart ?? (detected.Pieces.Count > 0 ? detected.StartMilliseconds : 0);
        int end = manualEnd ?? detected.EndMilliseconds ?? fileEnd;
        return new SongSegment(PiecesBetween(start, end), detected.Method, 1, true, mismatch);
    }

    /// <summary>
    /// One piece from the start to the end of the song in the file; a negative start means that the file starts that
    /// far into the song. Empty if the end does not lie after the start and after the beginning of the file.
    /// </summary>
    public static IReadOnlyList<SongPiece> PiecesBetween(int start, int end)
    {
        int fileStart = Math.Max(0, start);
        if (end <= fileStart)
        {
            return [];
        }

        return [new SongPiece(fileStart, fileStart - start, end - fileStart)];
    }
}
