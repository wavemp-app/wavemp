using WaveMP.Core.Analysis.Features;
using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Detection;

/// <summary>
/// Finds where the song starts in a file from synchronized lyrics: their timestamps belong to a recording of the song
/// (of known duration if they come from LRCLIB), and sung lines start where new sound starts in the bands of the voice.
/// The lyrics are shifted over the file; the shift at which the most lines start on such onsets is the start of the
/// song. How clearly that shift stands out against all others decides the confidence.
/// </summary>
public static class LyricsAligner
{
    /// <summary>
    /// At least this many lines are needed.
    /// </summary>
    internal const int MinLines = 4;

    /// <summary>
    /// Highest confidence of an alignment with lyrics; an alignment with the other recording is more reliable.
    /// </summary>
    internal const double MaxConfidence = 0.85;

    /// <summary>
    /// A file at most this much longer than the recording of the lyrics is the same recording; nothing to shift.
    /// </summary>
    internal static readonly TimeSpan SameLength = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The search also tries shifts this far beyond the plausible ones (frames).
    /// </summary>
    private const int Margin = 2 * SoundFeatures.FramesPerSecond;

    /// <summary>
    /// A sound shows as onset this many frames before it starts, because the window of a frame reaches ahead.
    /// </summary>
    private const int LatencyFrames = 2;

    /// <summary>
    /// An onset counts for a line if it lies this many frames before ...
    /// </summary>
    private const int Before = 5;

    /// <summary>
    /// ... or after the time of the line.
    /// </summary>
    private const int After = 20;

    /// <summary>
    /// Aligns the lines with the file; <c>null</c> without enough lines, without onsets or if the file has the length
    /// of the recording of the lyrics.
    /// </summary>
    /// <param name="features">The sound of the file.</param>
    /// <param name="lineStarts">When the lines start in the recording of the lyrics.</param>
    /// <param name="recordingDuration">Duration of that recording, if known; the song then ends that much after its
    /// start.</param>
    public static DetectedSegment? Align(
        SoundFeatures features,
        IReadOnlyList<TimeSpan> lineStarts,
        TimeSpan? recordingDuration)
    {
        int[] lines = [.. lineStarts
            .Select(start => (int)Math.Round(start.TotalSeconds * SoundFeatures.FramesPerSecond))
            .Where(frame => frame >= 0)
            .Distinct()
            .Order()];
        int frames = features.FrameCount;
        if (lines.Length < MinLines || frames == 0)
        {
            return null;
        }

        int? recordingFrames = recordingDuration is { } duration && duration > TimeSpan.Zero
            ? (int)Math.Round(duration.TotalSeconds * SoundFeatures.FramesPerSecond)
            : null;
        if (recordingFrames is { } length
            && Math.Abs(frames - length) <= SameLength.TotalSeconds * SoundFeatures.FramesPerSecond)
        {
            return null;
        }

        float[] strength = Strength(features.Onsets);
        if (strength.Length == 0)
        {
            return null;
        }

        int low = -Margin;
        int high = (recordingFrames is { } known ? frames - known : frames - lines[^1]) + Margin;
        if (high <= low)
        {
            return null;
        }

        double[] scores = new double[high - low + 1];
        for (int shift = low; shift <= high; shift++)
        {
            double sum = 0;
            foreach (int line in lines)
            {
                int frame = shift + line;
                if (frame >= 0 && frame < strength.Length)
                {
                    sum += strength[frame];
                }
            }

            scores[shift - low] = sum / lines.Length;
        }

        int bestIndex = Array.IndexOf(scores, scores.Max());
        double best = scores[bestIndex];
        double mean = scores.Average();
        double deviation = Math.Sqrt(scores.Average(score => (score - mean) * (score - mean)));
        double second = scores
            .Where((_, index) => Math.Abs(index - bestIndex) > SoundFeatures.FramesPerSecond / 2)
            .DefaultIfEmpty(0)
            .Max();
        if (best <= 0 || deviation <= 1e-9)
        {
            return null;
        }

        double standing = Math.Clamp(((best - mean) / deviation - 3) / 4, 0, 1);
        double margin = Math.Clamp((best - second) / (0.15 * best), 0, 1);
        double confidence = MaxConfidence * standing * margin;

        int startFrame = Refine(features.Onsets, lines, bestIndex + low) + LatencyFrames;
        int start = startFrame * (1000 / SoundFeatures.FramesPerSecond);
        int fileEnd = frames * (1000 / SoundFeatures.FramesPerSecond);
        int end = recordingFrames is { } recording
            ? Math.Min(fileEnd, start + (recording * (1000 / SoundFeatures.FramesPerSecond)))
            : fileEnd;
        IReadOnlyList<SongPiece> pieces = SongSegment.PiecesBetween(start, end);
        return pieces.Count == 0 ? null : new DetectedSegment(pieces, SongSegmentMethod.Lyrics, confidence);
    }

    /// <summary>
    /// The shift to the frame: the coarse search finds a range of shifts that all reach the onsets of the lines; here
    /// the lines are put onto the onsets themselves.
    /// </summary>
    private static int Refine(float[] onsets, int[] lines, int coarse)
    {
        double best = double.NegativeInfinity;
        int bestShift = coarse;
        for (int shift = coarse - Before; shift <= coarse + After; shift++)
        {
            double sum = 0;
            foreach (int line in lines)
            {
                int frame = shift + line;
                float strongest = 0;
                for (int index = Math.Max(0, frame - 1); index <= Math.Min(onsets.Length - 1, frame + 1); index++)
                {
                    strongest = Math.Max(strongest, onsets[index]);
                }

                sum += strongest;
            }

            if (sum > best)
            {
                best = sum;
                bestShift = shift;
            }
        }

        return bestShift;
    }

    /// <summary>
    /// The onsets as strength per frame: the strongest onset near the frame, relative to the strong onsets of the file.
    /// </summary>
    private static float[] Strength(float[] onsets)
    {
        if (onsets.Length == 0)
        {
            return [];
        }

        // The strong onsets of the file; in sparse files (few onsets at all) at least a third of the strongest one, so
        // tiny changes of a steady sound do not count as onsets.
        float[] sorted = [.. onsets.Order()];
        float reference = Math.Max(sorted[(int)(sorted.Length * 0.98)], sorted[^1] / 3);
        if (reference <= 0)
        {
            return [];
        }

        float[] strength = new float[onsets.Length];
        for (int frame = 0; frame < onsets.Length; frame++)
        {
            float strongest = 0;
            int from = Math.Max(0, frame - Before);
            int to = Math.Min(onsets.Length - 1, frame + After);
            for (int index = from; index <= to; index++)
            {
                strongest = Math.Max(strongest, onsets[index]);
            }

            strength[frame] = Math.Min(1, strongest / reference);
        }

        return strength;
    }
}
