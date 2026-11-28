namespace WaveMP.Data.Entities;

/// <summary>
/// What the song analysis found in the sound of a media file (an audio file or the sound track of a video): where the
/// real song lies in the file (its song segment) and how loud the file is. One row per analyzed file, removed with its
/// file; a file whose size or time of change differs from <see cref="FileSize"/> and <see cref="FileModifiedAt"/>, or
/// that was analyzed by an older <see cref="Version"/>, is analyzed again. Corrections of the admin are kept with the
/// track (see <see cref="Track.AudioSongStartMilliseconds"/>), so no analysis ever overwrites them.
/// </summary>
public class MediaFileAnalysis
{
    public required Guid MediaFileId { get; set; }

    public MediaFile? MediaFile { get; set; }

    /// <summary>
    /// Version of the analysis that analyzed the file. Files analyzed by an older version are analyzed again.
    /// </summary>
    public required int Version { get; set; }

    /// <summary>
    /// Size of the file when it was analyzed.
    /// </summary>
    public required long FileSize { get; set; }

    /// <summary>
    /// Time of the last change of the file when it was analyzed.
    /// </summary>
    public required DateTimeOffset FileModifiedAt { get; set; }

    public required DateTimeOffset AnalyzedAt { get; set; }

    public required SongAnalysisStatus Status { get; set; }

    /// <summary>
    /// How the song segment was found. Stored as its name.
    /// </summary>
    public required SongSegmentMethod Method { get; set; }

    /// <summary>
    /// How sure the analysis is about the song segment, between 0 and 1; <c>null</c> for
    /// <see cref="SongSegmentMethod.WholeFile"/>.
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Whether the file was compared with the other recording of the track (the audio file next to a video) and did
    /// not match it, e.g. a live version or a remix; the admin gets a hint.
    /// </summary>
    public bool Mismatch { get; set; }

    /// <summary>
    /// The parts of the file that belong to the song, in the order of the file, each with its position in the file and
    /// in the song; empty for <see cref="SongSegmentMethod.WholeFile"/>, where the whole file is the song.
    /// </summary>
    public List<SongPiece> Pieces { get; set; } = [];

    /// <summary>
    /// Integrated loudness of the sound in LUFS (EBU R 128); <c>null</c> if it could not be measured.
    /// </summary>
    public double? LoudnessLufs { get; set; }
}
