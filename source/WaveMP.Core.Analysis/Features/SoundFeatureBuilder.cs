namespace WaveMP.Core.Analysis.Features;

/// <summary>
/// Builds <see cref="SoundFeatures"/> from mono samples at <see cref="SoundFeatures.SampleRate"/> that arrive in
/// pieces of any size, e.g. while ffmpeg decodes a file: every 10 milliseconds a window of 32 milliseconds (Hann) is
/// transformed (FFT) and its energy summed up per band.
/// </summary>
public sealed class SoundFeatureBuilder
{
    /// <summary>
    /// Samples per window (32 ms).
    /// </summary>
    internal const int Window = 256;

    /// <summary>
    /// Samples from one frame to the next (10 ms).
    /// </summary>
    internal const int Hop = SoundFeatures.SampleRate / SoundFeatures.FramesPerSecond;

    /// <summary>
    /// Lower edges of the bands in Hz and the upper edge of the last one.
    /// </summary>
    private static readonly double[] _bandEdges = [60, 150, 300, 600, 1000, 1600, 2400, 3200, 4000];

    /// <summary>
    /// Energy added before the logarithm, so silence gives a finite value (about -90 dB).
    /// </summary>
    private const double Floor = 1e-9;

    private static readonly float[] _hann = CreateHann();
    private static readonly int[] _bandOfBin = CreateBandOfBin();

    private readonly List<float> _bands = [];
    private readonly List<float> _energy = [];
    private readonly float[] _real = new float[Window];
    private readonly float[] _imaginary = new float[Window];
    private readonly double[] _bandEnergy = new double[SoundFeatures.BandCount];
    private float[] _pending = new float[Window * 32];
    private int _pendingCount;

    /// <summary>
    /// Adds the next samples.
    /// </summary>
    public void Add(ReadOnlySpan<float> samples)
    {
        while (samples.Length > 0)
        {
            int free = _pending.Length - _pendingCount;
            if (free == 0)
            {
                Array.Resize(ref _pending, _pending.Length * 2);
                free = _pending.Length - _pendingCount;
            }

            int take = Math.Min(free, samples.Length);
            samples[..take].CopyTo(_pending.AsSpan(_pendingCount));
            _pendingCount += take;
            samples = samples[take..];
            ProcessPending();
        }
    }

    /// <summary>
    /// The features of all samples added so far.
    /// </summary>
    public SoundFeatures Build()
    {
        float[] bands = [.. _bands];
        float[] energy = [.. _energy];
        int frames = energy.Length;
        float[] onsets = new float[frames];
        for (int frame = 1; frame < frames; frame++)
        {
            float sum = 0;
            for (int band = SoundFeatures.FirstVoiceBand; band <= SoundFeatures.LastVoiceBand; band++)
            {
                float rise = bands[(frame * SoundFeatures.BandCount) + band]
                    - bands[((frame - 1) * SoundFeatures.BandCount) + band];
                if (rise > 0)
                {
                    sum += rise;
                }
            }

            onsets[frame] = sum;
        }

        return new SoundFeatures(bands, energy, onsets);
    }

    private void ProcessPending()
    {
        int position = 0;
        while (_pendingCount - position >= Window)
        {
            AddFrame(_pending.AsSpan(position, Window));
            position += Hop;
        }

        if (position > 0)
        {
            int rest = _pendingCount - position;
            Array.Copy(_pending, position, _pending, 0, rest);
            _pendingCount = rest;
        }
    }

    private void AddFrame(ReadOnlySpan<float> samples)
    {
        for (int index = 0; index < Window; index++)
        {
            _real[index] = samples[index] * _hann[index];
            _imaginary[index] = 0;
        }

        Fft.Transform(_real, _imaginary);
        Array.Clear(_bandEnergy);
        double total = 0;
        for (int bin = 1; bin <= Window / 2; bin++)
        {
            double power = ((double)_real[bin] * _real[bin]) + ((double)_imaginary[bin] * _imaginary[bin]);
            total += power;
            int band = _bandOfBin[bin];
            if (band >= 0)
            {
                _bandEnergy[band] += power;
            }
        }

        // The window of a full-scale sine sums up to about (Window / 4)^2 in its bin.
        const double Reference = (Window / 4.0) * (Window / 4.0);
        for (int band = 0; band < SoundFeatures.BandCount; band++)
        {
            _bands.Add((float)Math.Log10((_bandEnergy[band] / Reference) + Floor));
        }

        _energy.Add((float)(10 * Math.Log10((total / Reference) + Floor)));
    }

    private static float[] CreateHann()
    {
        float[] window = new float[Window];
        for (int index = 0; index < Window; index++)
        {
            window[index] = (float)(0.5 - (0.5 * Math.Cos(2 * Math.PI * index / (Window - 1))));
        }

        return window;
    }

    private static int[] CreateBandOfBin()
    {
        int[] bands = new int[(Window / 2) + 1];
        double binWidth = (double)SoundFeatures.SampleRate / Window;
        for (int bin = 0; bin < bands.Length; bin++)
        {
            double frequency = bin * binWidth;
            bands[bin] = -1;
            for (int band = 0; band < SoundFeatures.BandCount; band++)
            {
                if (frequency >= _bandEdges[band] && frequency < _bandEdges[band + 1])
                {
                    bands[bin] = band;
                    break;
                }
            }
        }

        // The highest bin (Nyquist) belongs to the last band.
        bands[^1] = SoundFeatures.BandCount - 1;
        return bands;
    }
}
