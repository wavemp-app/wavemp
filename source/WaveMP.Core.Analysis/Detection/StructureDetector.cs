using WaveMP.Core.Analysis.Features;
using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Detection;

/// <summary>
/// Guesses intro and outro from the structure of a file: speech, noise or a scene at the start or end of a music video
/// is usually separated from the song by a moment of silence. A silence after some sound near the start ends the intro,
/// a silence followed by some sound near the end starts the outro. Silence at the very start or end alone is part of
/// the song (as on the album). The least reliable detection; its result is only a hint for the admin.
/// </summary>
public static class StructureDetector
{
    /// <summary>
    /// Confidence of every result: below <see cref="SongSegment.ConfidentFrom"/>, so the players keep the whole file
    /// until the admin confirms it.
    /// </summary>
    public const double Confidence = 0.3;

    /// <summary>
    /// Frames quieter than the loud parts of the file by this many dB count as silence.
    /// </summary>
    private const double SilenceBelowDb = 35;

    /// <summary>
    /// Shortest silence between intro or outro and the song (frames).
    /// </summary>
    private const int MinSilence = SoundFeatures.FramesPerSecond * 2 / 5;

    /// <summary>
    /// Least sound before a silence for it to end an intro, and after it to start an outro (frames).
    /// </summary>
    private const int MinSound = SoundFeatures.FramesPerSecond;

    /// <summary>
    /// Intro and outro are looked for in this share of the file at its start and end ...
    /// </summary>
    private const double EdgeShare = 0.3;

    /// <summary>
    /// ... but at most this far from the start or end (frames).
    /// </summary>
    private const int MaxEdge = 90 * SoundFeatures.FramesPerSecond;

    /// <summary>
    /// The song between intro and outro; <c>null</c> if the file shows neither.
    /// </summary>
    public static DetectedSegment? Detect(SoundFeatures features)
    {
        int frames = features.FrameCount;
        if (frames < 10 * SoundFeatures.FramesPerSecond)
        {
            return null;
        }

        float[] sorted = [.. features.EnergyDb.Order()];
        double loud = sorted[(int)(sorted.Length * 0.9)];
        double threshold = loud - SilenceBelowDb;
        int edge = Math.Min(MaxEdge, (int)(frames * EdgeShare));
        List<(int Start, int End)> silences = Silences(features.EnergyDb, threshold);

        int start = 0;
        foreach ((int silenceStart, int silenceEnd) in silences)
        {
            bool soundBefore = CountSound(features.EnergyDb, 0, silenceStart, threshold) >= MinSound;
            if (silenceEnd <= edge && soundBefore)
            {
                start = silenceEnd;
            }
        }

        int end = frames;
        foreach ((int silenceStart, int silenceEnd) in silences.AsEnumerable().Reverse())
        {
            bool soundAfter = CountSound(features.EnergyDb, silenceEnd, frames, threshold) >= MinSound;
            if (silenceStart >= frames - edge && silenceStart > start && soundAfter)
            {
                end = silenceStart;
            }
        }

        if (start == 0 && end == frames)
        {
            return null;
        }

        int unit = 1000 / SoundFeatures.FramesPerSecond;
        IReadOnlyList<SongPiece> pieces = SongSegment.PiecesBetween(start * unit, end * unit);
        return pieces.Count == 0 ? null : new DetectedSegment(pieces, SongSegmentMethod.Structure, Confidence);
    }

    /// <summary>
    /// Runs of silent frames of at least <see cref="MinSilence"/>, in their order.
    /// </summary>
    private static List<(int Start, int End)> Silences(float[] energy, double threshold)
    {
        List<(int, int)> silences = [];
        int runStart = -1;
        for (int frame = 0; frame <= energy.Length; frame++)
        {
            bool silent = frame < energy.Length && energy[frame] < threshold;
            if (silent && runStart < 0)
            {
                runStart = frame;
            }
            else if (!silent && runStart >= 0)
            {
                if (frame - runStart >= MinSilence)
                {
                    silences.Add((runStart, frame));
                }

                runStart = -1;
            }
        }

        return silences;
    }

    private static int CountSound(float[] energy, int from, int to, double threshold)
    {
        int count = 0;
        for (int frame = from; frame < to; frame++)
        {
            if (energy[frame] >= threshold)
            {
                count++;
            }
        }

        return count;
    }
}
