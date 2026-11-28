using AwesomeAssertions;
using WaveMP.Core.Analysis.Features;
using WaveMP.Core.Analysis.Tests.Sounds;

namespace WaveMP.Core.Analysis.Tests.Features;

[TestFixture]
public class SoundFeatureBuilderTests
{
    [Test]
    public void Build_TenSeconds_HasOneFramePerTenMilliseconds()
    {
        SoundFeatures features = SyntheticSound.Features(SyntheticSound.Melody(10, 1));

        features.FrameCount.Should().BeInRange(997, 1000);
        features.DurationSeconds.Should().BeApproximately(10, 0.05);
        features.Bands.Should().HaveCount(features.FrameCount * SoundFeatures.BandCount);
        features.Onsets.Should().HaveCount(features.FrameCount);
    }

    [Test]
    public void Add_InPiecesOfAnySize_GivesTheSameFeaturesAsAtOnce()
    {
        float[] samples = SyntheticSound.Melody(3, 2);
        SoundFeatureBuilder builder = new();
        int position = 0;
        foreach (int size in Enumerable.Repeat(new[] { 1, 7, 333, 4096, 80 }, 1000).SelectMany(sizes => sizes))
        {
            if (position >= samples.Length)
            {
                break;
            }

            int take = Math.Min(size, samples.Length - position);
            builder.Add(samples.AsSpan(position, take));
            position += take;
        }

        SoundFeatures pieces = builder.Build();
        var whole = SoundFeatures.FromSamples(samples);

        pieces.Bands.Should().Equal(whole.Bands);
        pieces.EnergyDb.Should().Equal(whole.EnergyDb);
    }

    [Test]
    public void Build_Tone_PutsItsEnergyIntoItsBand()
    {
        float[] samples = new float[SoundFeatures.SampleRate];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = 0.5f * (float)Math.Sin(2 * Math.PI * 800 * index / SoundFeatures.SampleRate);
        }

        var features = SoundFeatures.FromSamples(samples);

        // 800 Hz lies in the band from 600 to 1000 Hz (index 3).
        int frame = features.FrameCount / 2;
        float[] bands = [.. Enumerable.Range(0, SoundFeatures.BandCount).Select(band => features.Band(frame, band))];
        Array.IndexOf(bands, bands.Max()).Should().Be(3);
    }

    [Test]
    public void Build_SilenceAndSound_TellsThemApartByEnergy()
    {
        SoundFeatures features = SyntheticSound.Features(SyntheticSound.Concat(
            SyntheticSound.Silence(1),
            SyntheticSound.Melody(1, 3)));

        features.EnergyDb[50].Should().BeLessThan(-60);
        features.EnergyDb[150].Should().BeGreaterThan(-30);
    }

    [Test]
    public void Build_LineStarts_ShowOnsets()
    {
        SoundFeatures features = SyntheticSound.Features(SyntheticSound.Bursts(4, [1.0, 2.5]));

        float[] onsets = features.Onsets;
        float strongest = onsets.Max();
        onsets.Skip(95).Take(10).Max().Should().BeGreaterThan(strongest / 2);
        onsets.Skip(245).Take(10).Max().Should().BeGreaterThan(strongest / 2);
        onsets.Skip(150).Take(50).Max().Should().BeLessThan(strongest / 4);
    }

    [Test]
    public void Transform_Sine_HasItsPeakAtItsBin()
    {
        float[] real = new float[64];
        float[] imaginary = new float[64];
        for (int index = 0; index < real.Length; index++)
        {
            real[index] = (float)Math.Cos(2 * Math.PI * 5 * index / real.Length);
        }

        Fft.Transform(real, imaginary);

        float[] magnitudes = [.. real.Zip(imaginary, (re, im) => MathF.Sqrt((re * re) + (im * im))).Take(32)];
        Array.IndexOf(magnitudes, magnitudes.Max()).Should().Be(5);
        magnitudes[5].Should().BeApproximately(32, 0.01f);
    }

    [Test]
    public void Transform_LengthNotAPowerOfTwo_Throws()
    {
        Action transform = () => Fft.Transform(new float[6], new float[6]);

        transform.Should().Throw<ArgumentException>();
    }
}
