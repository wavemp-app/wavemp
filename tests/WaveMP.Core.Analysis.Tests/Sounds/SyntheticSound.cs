using WaveMP.Core.Analysis.Features;

namespace WaveMP.Core.Analysis.Tests.Sounds;

/// <summary>
/// Synthetic mono sound at <see cref="SoundFeatures.SampleRate"/> for the detections: melodies of random notes that
/// never repeat, noise, silence and short tone bursts, which tests put together to songs with intros, outros and cut
/// parts.
/// </summary>
internal static class SyntheticSound
{
    private const int Rate = SoundFeatures.SampleRate;

    /// <summary>
    /// A melody of two voices with random notes (a quarter and a third of a second long) and random loudness; the
    /// same seed gives the same melody.
    /// </summary>
    public static float[] Melody(double seconds, int seed, float amplitude = 0.4f)
    {
        float[] samples = new float[(int)(seconds * Rate)];
        AddVoice(samples, new Random(seed), Rate / 4, 200, 1400, amplitude);
        AddVoice(samples, new Random(seed + 1000), Rate / 3, 600, 3000, amplitude * 0.6f);
        return samples;
    }

    /// <summary>
    /// White noise whose loudness changes every tenth of a second, e.g. as the talk of an intro.
    /// </summary>
    public static float[] Noise(double seconds, int seed, float amplitude = 0.2f)
    {
        Random random = new(seed);
        float[] samples = new float[(int)(seconds * Rate)];
        float level = amplitude;
        for (int index = 0; index < samples.Length; index++)
        {
            if (index % (Rate / 10) == 0)
            {
                level = amplitude * (0.3f + (0.7f * (float)random.NextDouble()));
            }

            samples[index] = level * ((2 * (float)random.NextDouble()) - 1);
        }

        return samples;
    }

    public static float[] Silence(double seconds)
    {
        return new float[(int)(seconds * Rate)];
    }

    /// <summary>
    /// A steady low tone (below the bands of the voice) with short bursts of a voice-like tone at the given times,
    /// e.g. a song whose lines start at these times.
    /// </summary>
    public static float[] Bursts(double seconds, IEnumerable<double> starts, double burstSeconds = 0.8)
    {
        float[] samples = new float[(int)(seconds * Rate)];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = 0.05f * (float)Math.Sin(2 * Math.PI * 80 * index / Rate);
        }

        foreach (double start in starts)
        {
            int from = (int)(start * Rate);
            int length = (int)(burstSeconds * Rate);
            for (int offset = 0; offset < length && from + offset < samples.Length; offset++)
            {
                if (from + offset < 0)
                {
                    continue;
                }

                double t = (double)offset / Rate;
                double fade = Math.Min(1, Math.Min(t / 0.01, (burstSeconds - t) / 0.05));
                samples[from + offset] += (float)(0.4 * fade
                    * (Math.Sin(2 * Math.PI * 700 * t) + (0.5 * Math.Sin(2 * Math.PI * 1500 * t))));
            }
        }

        return samples;
    }

    public static float[] Concat(params float[][] parts)
    {
        return [.. parts.SelectMany(part => part)];
    }

    public static float[] Slice(float[] samples, double fromSeconds, double toSeconds)
    {
        int from = (int)(fromSeconds * Rate);
        int to = Math.Min(samples.Length, (int)(toSeconds * Rate));
        return samples[from..to];
    }

    public static float[] Gain(float[] samples, float factor)
    {
        return [.. samples.Select(sample => sample * factor)];
    }

    public static SoundFeatures Features(float[] samples)
    {
        return SoundFeatures.FromSamples(samples);
    }

    private static void AddVoice(
        float[] samples,
        Random random,
        int noteLength,
        double low,
        double high,
        float amplitude)
    {
        double phase = 0;
        double frequency = low;
        float level = amplitude;
        int fade = Rate / 200;
        for (int index = 0; index < samples.Length; index++)
        {
            int inNote = index % noteLength;
            if (inNote == 0)
            {
                frequency = low * Math.Pow(high / low, random.NextDouble());
                level = amplitude * (0.2f + (0.8f * (float)random.NextDouble()));
            }

            float envelope = Math.Min(1f, Math.Min((float)inNote / fade, (float)(noteLength - inNote) / fade));
            phase += 2 * Math.PI * frequency / Rate;
            samples[index] += level * envelope * (float)Math.Sin(phase);
        }
    }
}
