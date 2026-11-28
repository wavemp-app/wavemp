namespace WaveMP.Data.Entities;

/// <summary>
/// A part of a media file that belongs to the song: <see cref="DurationMilliseconds"/> long, starting at
/// <see cref="FileStartMilliseconds"/> in the file and at <see cref="SongStartMilliseconds"/> in the song (the time of
/// the reference recording, e.g. the album version, which synchronized lyrics follow). A video with an intro has one
/// piece that starts later in the file than in the song; a video with a cut part has two pieces.
/// </summary>
public sealed record SongPiece(int FileStartMilliseconds, int SongStartMilliseconds, int DurationMilliseconds);
