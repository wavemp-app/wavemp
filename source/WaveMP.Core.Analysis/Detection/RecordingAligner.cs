using WaveMP.Core.Analysis.Features;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Detection;

/// <summary>
/// Finds where a recording (the reference, e.g. the album version in the audio file) is contained in a second
/// recording of the same song (the target, e.g. the sound of the music video), also if the target cut parts out or
/// added scenes. The reference is cut into overlapping chunks of a few seconds; each chunk is searched in the whole
/// target by the correlation of the loudness of the frequency bands, first coarsely, then to the frame. Chunks that
/// agree on the shift form runs, and every run becomes a piece; where two runs meet, the exact cut is where the frames
/// fit the one shift better than the other. Correlation does not care about loudness, so a louder or quieter master
/// matches as well. Too few or too weak matches count as another recording.
/// </summary>
public static class RecordingAligner
{
    /// <summary>
    /// Frames per frame of the coarse search (50 ms).
    /// </summary>
    internal const int CoarseFactor = 5;

    /// <summary>
    /// Length of a chunk in coarse frames (6 s).
    /// </summary>
    internal const int ChunkLength = 120;

    /// <summary>
    /// Distance between the starts of two chunks in coarse frames (3 s).
    /// </summary>
    internal const int ChunkHop = 60;

    /// <summary>
    /// A chunk with at least this correlation at its best shift counts as found.
    /// </summary>
    internal const double GoodScore = 0.6;

    /// <summary>
    /// The recordings match if at least this share of the chunks with sound was found ...
    /// </summary>
    internal const double MinGoodShare = 0.5;

    /// <summary>
    /// ... and the pieces cover at least this share of the reference.
    /// </summary>
    internal const double MinCoverage = 0.5;

    /// <summary>
    /// Chunks whose shifts differ by at most this many frames (40 ms) belong to the same run.
    /// </summary>
    internal const int LagTolerance = 4;

    /// <summary>
    /// The fine search looks this many frames around the result of the coarse search.
    /// </summary>
    private const int RefineRadius = 2 * CoarseFactor;

    /// <summary>
    /// Bands that change less than this (log10 of the energy) within a chunk carry no information.
    /// </summary>
    private const double MinDeviation = 0.05;

    private const int Bands = SoundFeatures.BandCount;

    /// <summary>
    /// Aligns the reference with the target; <c>null</c> if one of them is too short to compare (shorter than a chunk).
    /// </summary>
    public static RecordingAlignment? Align(SoundFeatures reference, SoundFeatures target)
    {
        int chunkFrames = ChunkLength * CoarseFactor;
        if (reference.FrameCount < chunkFrames || target.FrameCount < chunkFrames)
        {
            return null;
        }

        List<Chunk> chunks = FindChunks(reference, target);
        Chunk[] informative = [.. chunks.Where(chunk => chunk.Informative)];
        if (informative.Length == 0)
        {
            return null;
        }

        Chunk[] good = [.. informative.Where(chunk => chunk.Score >= GoodScore)];
        double goodShare = (double)good.Length / informative.Length;
        double meanScore = good.Length == 0 ? 0 : good.Average(chunk => chunk.Score);
        double confidence = Math.Clamp(meanScore * goodShare, 0, 1);
        List<Run> runs = FindRuns(good);
        if (runs.Count == 0)
        {
            return new RecordingAlignment(false, confidence, []);
        }

        List<SongPiece> pieces = BuildPieces(reference, target, runs);
        double covered = pieces.Sum(piece => (double)piece.DurationMilliseconds);
        double coverage = covered / (reference.DurationSeconds * 1000);
        bool matches = goodShare >= MinGoodShare && meanScore >= GoodScore && coverage >= MinCoverage;
        return matches
            ? new RecordingAlignment(true, confidence, pieces)
            : new RecordingAlignment(false, confidence, []);
    }

    private static List<Chunk> FindChunks(SoundFeatures reference, SoundFeatures target)
    {
        float[][] coarseReference = Coarse(reference);
        float[][] coarseTarget = Coarse(target);
        int targetLength = coarseTarget[0].Length;
        double[][] sums = new double[Bands][];
        double[][] squares = new double[Bands][];
        for (int band = 0; band < Bands; band++)
        {
            sums[band] = new double[targetLength + 1];
            squares[band] = new double[targetLength + 1];
            for (int frame = 0; frame < targetLength; frame++)
            {
                double value = coarseTarget[band][frame];
                sums[band][frame + 1] = sums[band][frame] + value;
                squares[band][frame + 1] = squares[band][frame] + (value * value);
            }
        }

        List<Chunk> chunks = [];
        float[][] chunk = new float[Bands][];
        for (int band = 0; band < Bands; band++)
        {
            chunk[band] = new float[ChunkLength];
        }

        for (int start = 0; start + ChunkLength <= coarseReference[0].Length; start += ChunkHop)
        {
            bool[] used = Normalize(coarseReference, start, ChunkLength, chunk);
            int usedBands = used.Count(value => value);
            if (usedBands == 0)
            {
                chunks.Add(new Chunk(start * CoarseFactor, 0, 0, false));
                continue;
            }

            double best = double.NegativeInfinity;
            int bestStart = 0;
            for (int targetStart = 0; targetStart + ChunkLength <= targetLength; targetStart++)
            {
                double score = 0;
                for (int band = 0; band < Bands; band++)
                {
                    if (!used[band])
                    {
                        continue;
                    }

                    double sum = sums[band][targetStart + ChunkLength] - sums[band][targetStart];
                    double square = squares[band][targetStart + ChunkLength] - squares[band][targetStart];
                    double variance = (square / ChunkLength) - ((sum / ChunkLength) * (sum / ChunkLength));
                    if (variance <= 1e-9)
                    {
                        continue;
                    }

                    ReadOnlySpan<float> x = chunk[band];
                    ReadOnlySpan<float> y = coarseTarget[band].AsSpan(targetStart, ChunkLength);
                    double dot = 0;
                    for (int index = 0; index < ChunkLength; index++)
                    {
                        dot += x[index] * y[index];
                    }

                    score += dot / (ChunkLength * Math.Sqrt(variance));
                }

                score /= usedBands;
                if (score > best)
                {
                    best = score;
                    bestStart = targetStart;
                }
            }

            int referenceFrame = start * CoarseFactor;
            int coarseLag = (bestStart - start) * CoarseFactor;
            (int lag, double score) refined = Refine(reference, target, referenceFrame, coarseLag);
            chunks.Add(new Chunk(referenceFrame, refined.lag, refined.score, true));
        }

        return chunks;
    }

    /// <summary>
    /// The best shift to the frame around the result of the coarse search, with its correlation.
    /// </summary>
    private static (int Lag, double Score) Refine(
        SoundFeatures reference,
        SoundFeatures target,
        int referenceFrame,
        int coarseLag)
    {
        int length = ChunkLength * CoarseFactor;
        float[][] chunk = new float[Bands][];
        for (int band = 0; band < Bands; band++)
        {
            chunk[band] = new float[length];
            for (int index = 0; index < length; index++)
            {
                chunk[band][index] = reference.Band(referenceFrame + index, band);
            }
        }

        bool[] used = NormalizeInPlace(chunk);
        int usedBands = Math.Max(1, used.Count(value => value));
        double best = double.NegativeInfinity;
        int bestLag = coarseLag;
        float[] window = new float[length];
        for (int lag = coarseLag - RefineRadius; lag <= coarseLag + RefineRadius; lag++)
        {
            int targetStart = referenceFrame + lag;
            if (targetStart < 0 || targetStart + length > target.FrameCount)
            {
                continue;
            }

            double score = 0;
            for (int band = 0; band < Bands; band++)
            {
                if (!used[band])
                {
                    continue;
                }

                double sum = 0;
                double square = 0;
                for (int index = 0; index < length; index++)
                {
                    float value = target.Band(targetStart + index, band);
                    window[index] = value;
                    sum += value;
                    square += value * value;
                }

                double variance = (square / length) - ((sum / length) * (sum / length));
                if (variance <= 1e-9)
                {
                    continue;
                }

                double dot = 0;
                for (int index = 0; index < length; index++)
                {
                    dot += chunk[band][index] * window[index];
                }

                score += dot / (length * Math.Sqrt(variance));
            }

            score /= usedBands;
            if (score > best)
            {
                best = score;
                bestLag = lag;
            }
        }

        return (bestLag, double.IsNegativeInfinity(best) ? 0 : best);
    }

    /// <summary>
    /// Groups the found chunks into runs of the same shift, in the order of the reference. Single chunks between runs
    /// are left out as chance matches, and neighboring runs with the same shift are merged.
    /// </summary>
    private static List<Run> FindRuns(IReadOnlyList<Chunk> good)
    {
        List<List<Chunk>> groups = [];
        foreach (Chunk chunk in good.OrderBy(chunk => chunk.ReferenceFrame))
        {
            if (groups.Count > 0 && Math.Abs(chunk.Lag - Median(groups[^1])) <= LagTolerance)
            {
                groups[^1].Add(chunk);
            }
            else
            {
                groups.Add([chunk]);
            }
        }

        if (groups.Count > 1)
        {
            groups = [.. groups.Where(group => group.Count >= 2)];
        }

        List<List<Chunk>> merged = [];
        foreach (List<Chunk> group in groups)
        {
            if (merged.Count > 0 && Math.Abs(Median(merged[^1]) - Median(group)) <= LagTolerance)
            {
                merged[^1].AddRange(group);
            }
            else
            {
                merged.Add(group);
            }
        }

        List<Run> runs = [.. merged.Select(group => new Run(
            Median(group),
            group[0].ReferenceFrame,
            group[^1].ReferenceFrame,
            group.Sum(chunk => chunk.Score)))];
        return InOrder(runs);
    }

    /// <summary>
    /// The runs the target plays one after another: a video plays the parts of the song in their order, so a run that
    /// lies in the target before the end of an earlier run is a chance match with a repeated part of the song (e.g. a
    /// chorus, or a part the video cut out that sounds like another one). Of all chains of runs in order, the one
    /// with the most matching chunks is kept.
    /// </summary>
    private static List<Run> InOrder(List<Run> runs)
    {
        if (runs.Count == 0)
        {
            return runs;
        }

        int chunkFrames = ChunkLength * CoarseFactor;
        double[] best = new double[runs.Count];
        int[] previous = new int[runs.Count];
        for (int index = 0; index < runs.Count; index++)
        {
            best[index] = runs[index].Weight;
            previous[index] = -1;
            int start = runs[index].FirstFrame + runs[index].Lag;
            for (int before = 0; before < index; before++)
            {
                int end = runs[before].LastFrame + chunkFrames + runs[before].Lag;
                bool follows = start >= end - (chunkFrames / 2);
                if (follows && best[before] + runs[index].Weight > best[index])
                {
                    best[index] = best[before] + runs[index].Weight;
                    previous[index] = before;
                }
            }
        }

        List<Run> chain = [];
        for (int index = Array.IndexOf(best, best.Max()); index >= 0; index = previous[index])
        {
            chain.Insert(0, runs[index]);
        }

        // Neighbors left with the same shift are one run.
        List<Run> merged = [];
        foreach (Run run in chain)
        {
            if (merged.Count > 0 && Math.Abs(merged[^1].Lag - run.Lag) <= LagTolerance)
            {
                merged[^1] = merged[^1] with { LastFrame = run.LastFrame, Weight = merged[^1].Weight + run.Weight };
            }
            else
            {
                merged.Add(run);
            }
        }

        return merged;
    }

    /// <summary>
    /// Turns the runs into pieces: the first one reaches back to the start of the reference, the last one to its end
    /// (as far as the target reaches), and where two runs meet, the cut lies where the frames fit the one shift better
    /// than the other.
    /// </summary>
    private static List<SongPiece> BuildPieces(SoundFeatures reference, SoundFeatures target, List<Run> runs)
    {
        int chunkFrames = ChunkLength * CoarseFactor;
        float[] normalizedReference = NormalizeFrames(reference);
        float[] normalizedTarget = NormalizeFrames(target);

        // Song start and end of every run in frames of the reference.
        int[] starts = new int[runs.Count];
        int[] ends = new int[runs.Count];
        starts[0] = Math.Max(0, -runs[0].Lag);
        ends[^1] = Math.Min(reference.FrameCount, target.FrameCount - runs[^1].Lag);
        for (int index = 1; index < runs.Count; index++)
        {
            Run before = runs[index - 1];
            Run after = runs[index];
            if (after.Lag > before.Lag)
            {
                // The target added something: the cut lies in the reference, both pieces meet there.
                int low = before.LastFrame;
                int high = Math.Min(after.FirstFrame + chunkFrames, reference.FrameCount);
                int split = BestSplit(
                    low,
                    high,
                    frame => Similarity(normalizedReference, frame, normalizedTarget, frame + before.Lag),
                    frame => Similarity(normalizedReference, frame, normalizedTarget, frame + after.Lag));
                ends[index - 1] = split;
                starts[index] = split;
            }
            else
            {
                // The target cut something out: the cut lies in the target, both pieces meet there.
                int low = before.LastFrame + before.Lag;
                int high = Math.Min(after.FirstFrame + chunkFrames + after.Lag, target.FrameCount);
                int split = BestSplit(
                    low,
                    high,
                    frame => Similarity(normalizedReference, frame - before.Lag, normalizedTarget, frame),
                    frame => Similarity(normalizedReference, frame - after.Lag, normalizedTarget, frame));
                ends[index - 1] = split - before.Lag;
                starts[index] = split - after.Lag;
            }
        }

        List<SongPiece> pieces = [];
        for (int index = 0; index < runs.Count; index++)
        {
            int start = Math.Max(starts[index], Math.Max(0, -runs[index].Lag));
            int end = Math.Min(ends[index], Math.Min(reference.FrameCount, target.FrameCount - runs[index].Lag));
            if (end > start)
            {
                pieces.Add(new SongPiece(
                    ToMilliseconds(start + runs[index].Lag),
                    ToMilliseconds(start),
                    ToMilliseconds(end - start)));
            }
        }

        return pieces;
    }

    /// <summary>
    /// The frame between <paramref name="low"/> and <paramref name="high"/> from which on the frames fit the second
    /// shift better than the first one.
    /// </summary>
    private static int BestSplit(int low, int high, Func<int, double> first, Func<int, double> second)
    {
        if (high <= low)
        {
            return low;
        }

        double total = 0;
        double best = 0;
        int split = low;
        for (int frame = low; frame < high; frame++)
        {
            total += first(frame) - second(frame);
            if (total > best)
            {
                best = total;
                split = frame + 1;
            }
        }

        return split;
    }

    private static double Similarity(float[] reference, int referenceFrame, float[] target, int targetFrame)
    {
        if (referenceFrame < 0
            || targetFrame < 0
            || (referenceFrame + 1) * Bands > reference.Length
            || (targetFrame + 1) * Bands > target.Length)
        {
            return -1;
        }

        double dot = 0;
        for (int band = 0; band < Bands; band++)
        {
            dot += reference[(referenceFrame * Bands) + band] * target[(targetFrame * Bands) + band];
        }

        return dot / Bands;
    }

    /// <summary>
    /// The bands of all frames, each band normalized over the whole file (mean 0, deviation 1).
    /// </summary>
    private static float[] NormalizeFrames(SoundFeatures features)
    {
        float[] values = (float[])features.Bands.Clone();
        int frames = features.FrameCount;
        for (int band = 0; band < Bands; band++)
        {
            double sum = 0;
            double square = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                double value = values[(frame * Bands) + band];
                sum += value;
                square += value * value;
            }

            double mean = sum / Math.Max(1, frames);
            double deviation = Math.Sqrt(Math.Max(0, (square / Math.Max(1, frames)) - (mean * mean)));
            for (int frame = 0; frame < frames; frame++)
            {
                int index = (frame * Bands) + band;
                values[index] = deviation > 1e-6 ? (float)((values[index] - mean) / deviation) : 0;
            }
        }

        return values;
    }

    /// <summary>
    /// The bands averaged over <see cref="CoarseFactor"/> frames, band by band.
    /// </summary>
    private static float[][] Coarse(SoundFeatures features)
    {
        int length = features.FrameCount / CoarseFactor;
        float[][] bands = new float[Bands][];
        for (int band = 0; band < Bands; band++)
        {
            bands[band] = new float[length];
            for (int frame = 0; frame < length; frame++)
            {
                float sum = 0;
                for (int index = 0; index < CoarseFactor; index++)
                {
                    sum += features.Band((frame * CoarseFactor) + index, band);
                }

                bands[band][frame] = sum / CoarseFactor;
            }
        }

        return bands;
    }

    /// <summary>
    /// Copies a chunk of the coarse bands and normalizes each band (mean 0, deviation 1); returns which bands carry
    /// information.
    /// </summary>
    private static bool[] Normalize(float[][] bands, int start, int length, float[][] chunk)
    {
        for (int band = 0; band < Bands; band++)
        {
            Array.Copy(bands[band], start, chunk[band], 0, length);
        }

        return NormalizeInPlace(chunk);
    }

    private static bool[] NormalizeInPlace(float[][] chunk)
    {
        bool[] used = new bool[Bands];
        for (int band = 0; band < Bands; band++)
        {
            float[] values = chunk[band];
            double mean = values.Average(value => (double)value);
            double deviation = Math.Sqrt(values.Average(value => (value - mean) * (value - mean)));
            used[band] = deviation >= MinDeviation;
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = used[band] ? (float)((values[index] - mean) / deviation) : 0;
            }
        }

        return used;
    }

    private static int Median(List<Chunk> chunks)
    {
        int[] lags = [.. chunks.Select(chunk => chunk.Lag).Order()];
        return lags[lags.Length / 2];
    }

    private static int ToMilliseconds(int frames)
    {
        return frames * (1000 / SoundFeatures.FramesPerSecond);
    }

    /// <summary>
    /// A chunk of the reference: where it starts (frames), the shift to its best match in the target (frames, position
    /// in the target = position in the reference + lag), the correlation there and whether it has sound at all.
    /// </summary>
    private sealed record Chunk(int ReferenceFrame, int Lag, double Score, bool Informative);

    /// <summary>
    /// Chunks with the same shift: the shift, the starts of the first and the last chunk and the sum of their
    /// correlations.
    /// </summary>
    private sealed record Run(int Lag, int FirstFrame, int LastFrame, double Weight);
}
