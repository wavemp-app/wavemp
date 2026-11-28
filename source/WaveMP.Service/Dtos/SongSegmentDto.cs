namespace WaveMP.Service.Dtos;

/// <summary>
/// Where the real song lies in a file, without intro, outro and parts that were added. Clients map positions between
/// the audio file and the video with the pieces (song position = position of the file - fileStart + songStart within
/// a piece), skip intro and outro and show synchronized lyrics relative to the song. Clients use it only if confident
/// is true; otherwise the whole file counts.
/// </summary>
public sealed class SongSegmentDto
{
    /// <summary>
    /// Position of the file where the song starts in milliseconds; negative if the file starts that far into the song
    /// (only from corrections of the admin); 0 for the whole file.
    /// </summary>
    public required int StartMilliseconds { get; init; }

    /// <summary>
    /// Position of the file where the song ends in milliseconds; null if the whole file is the song.
    /// </summary>
    public int? EndMilliseconds { get; init; }

    /// <summary>
    /// The parts of the file that belong to the song, in the order of the file; empty if the whole file is the song.
    /// </summary>
    public required IReadOnlyList<SongPieceDto> Pieces { get; init; }

    /// <summary>
    /// How the segment was found.
    /// </summary>
    public required SongSegmentMethodDto Method { get; init; }

    /// <summary>
    /// How sure the analysis is, between 0 and 1; 1 for corrections of the admin, null for the whole file.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Whether clients use the segment: a correction of the admin, the whole file or a found segment with a confidence
    /// of at least 0.6.
    /// </summary>
    public required bool Confident { get; init; }

    /// <summary>
    /// Whether the admin corrected start or end; no later analysis changes them.
    /// </summary>
    public required bool Manual { get; init; }

    /// <summary>
    /// Whether the file did not match the other recording of the track (e.g. a live version); clients do not map
    /// positions between the audio file and the video then.
    /// </summary>
    public required bool Mismatch { get; init; }
}
