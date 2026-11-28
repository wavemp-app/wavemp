using WaveMP.Core.Analysis.Features;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Decoding;

/// <summary>
/// The sound of a file as the song analysis reads it.
/// </summary>
/// <param name="Status">Whether the sound was read, the file has none or it could not be read.</param>
/// <param name="Features">The features of the sound; <c>null</c> unless it was read.</param>
/// <param name="LoudnessLufs">Integrated loudness in LUFS (EBU R 128); <c>null</c> if it could not be measured.</param>
public sealed record SoundReading(
    SongAnalysisStatus Status,
    SoundFeatures? Features = null,
    double? LoudnessLufs = null)
{
    public static SoundReading NoSound { get; } = new(SongAnalysisStatus.NoSound);

    public static SoundReading Failed { get; } = new(SongAnalysisStatus.Failed);
}
