namespace WaveMP.Core.Analysis.Features;

/// <summary>
/// What the song analysis knows of the sound of a file: per frame of 10 milliseconds, the loudness of
/// <see cref="BandCount"/> frequency bands (log10 of their energy), the loudness of the whole frame in dB and how much
/// new sound starts in the bands of the human voice (onsets, for the alignment with lyrics). Built from mono sound at
/// <see cref="SampleRate"/> by <see cref="SoundFeatureBuilder"/>.
/// </summary>
public sealed class SoundFeatures
{
    /// <summary>
    /// Sample rate of the sound the features are built from; enough for the voice and most instruments.
    /// </summary>
    public const int SampleRate = 8000;

    /// <summary>
    /// Frames per second.
    /// </summary>
    public const int FramesPerSecond = 100;

    /// <summary>
    /// Number of frequency bands per frame.
    /// </summary>
    public const int BandCount = 8;

    /// <summary>
    /// The first and the last band (inclusive) of the voice, about 300 to 3200 Hz.
    /// </summary>
    public const int FirstVoiceBand = 2;

    public const int LastVoiceBand = 6;

    internal SoundFeatures(float[] bands, float[] energyDb, float[] onsets)
    {
        Bands = bands;
        EnergyDb = energyDb;
        Onsets = onsets;
        FrameCount = energyDb.Length;
    }

    /// <summary>
    /// Number of frames.
    /// </summary>
    public int FrameCount { get; }

    /// <summary>
    /// Duration of the sound in seconds.
    /// </summary>
    public double DurationSeconds => (double)FrameCount / FramesPerSecond;

    /// <summary>
    /// log10 of the energy of each band, frame after frame (<see cref="BandCount"/> values per frame).
    /// </summary>
    public float[] Bands { get; }

    /// <summary>
    /// Loudness of each frame in dB (relative to full scale).
    /// </summary>
    public float[] EnergyDb { get; }

    /// <summary>
    /// How much the bands of the voice rose against the frame before (sum of the rises), per frame.
    /// </summary>
    public float[] Onsets { get; }

    /// <summary>
    /// The value of a band in a frame.
    /// </summary>
    public float Band(int frame, int band)
    {
        return Bands[(frame * BandCount) + band];
    }

    /// <summary>
    /// Features of a sound given as samples at <see cref="SampleRate"/>; for tests and short sounds.
    /// </summary>
    public static SoundFeatures FromSamples(ReadOnlySpan<float> samples)
    {
        SoundFeatureBuilder builder = new();
        builder.Add(samples);
        return builder.Build();
    }
}
