using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Detection;

/// <summary>
/// A song segment one of the detections found in a file.
/// </summary>
/// <param name="Pieces">The parts of the file that belong to the song, in milliseconds.</param>
/// <param name="Method">How it was found.</param>
/// <param name="Confidence">How sure the detection is, between 0 and 1.</param>
public sealed record DetectedSegment(IReadOnlyList<SongPiece> Pieces, SongSegmentMethod Method, double Confidence);
