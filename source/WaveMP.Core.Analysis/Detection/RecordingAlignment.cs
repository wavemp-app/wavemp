using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Detection;

/// <summary>
/// Where a recording (the audio file) lies in a second recording of the same song (the sound of a video).
/// </summary>
/// <param name="Matches">Whether both are the same recording; <c>false</c> for another version such as a live version
/// or a remix, or a file without the song.</param>
/// <param name="Confidence">How well they match, between 0 and 1.</param>
/// <param name="Pieces">The parts of the second recording that contain the first one, with their positions in both;
/// empty if they do not match.</param>
public sealed record RecordingAlignment(bool Matches, double Confidence, IReadOnlyList<SongPiece> Pieces);
