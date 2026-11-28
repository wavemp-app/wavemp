namespace WaveMP.Service.Dtos;

/// <summary>
/// A part of a file that belongs to the song: from fileStartMilliseconds on, the file plays the song from
/// songStartMilliseconds on, for durationMilliseconds. The time of the song is the time of the reference recording
/// (the audio file) that synchronized lyrics follow.
/// </summary>
public sealed class SongPieceDto
{
    public required int FileStartMilliseconds { get; init; }

    public required int SongStartMilliseconds { get; init; }

    public required int DurationMilliseconds { get; init; }
}
