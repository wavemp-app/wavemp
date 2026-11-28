using AwesomeAssertions;
using WaveMP.Core.Analysis.Detection;
using WaveMP.Core.Analysis.Tests.Sounds;
using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Tests.Detection;

[TestFixture]
public class LyricsAlignerTests
{
    /// <summary>
    /// When the lines start in the recording of the lyrics, irregular like real lines.
    /// </summary>
    private static readonly double[] _lines = [2.0, 5.3, 9.1, 12.0, 16.4, 19.8, 24.6, 27.2, 31.9, 35.5, 38.1, 42.7];

    [Test]
    public void Align_VideoWithIntro_FindsTheStartOfTheSong()
    {
        // The song of 48 seconds starts after an intro of 10 seconds and is followed by an outro of 7 seconds.
        float[] video = SyntheticSound.Bursts(65, _lines.Select(line => line + 10));

        DetectedSegment? segment = LyricsAligner.Align(
            SyntheticSound.Features(video),
            [.. _lines.Select(TimeSpan.FromSeconds)],
            TimeSpan.FromSeconds(48));

        segment.Should().NotBeNull();
        segment!.Method.Should().Be(SongSegmentMethod.Lyrics);
        segment.Confidence.Should().BeGreaterThanOrEqualTo(SongSegment.ConfidentFrom);
        segment.Confidence.Should().BeLessThanOrEqualTo(LyricsAligner.MaxConfidence);
        segment.Pieces.Should().ContainSingle();
        segment.Pieces[0].FileStartMilliseconds.Should().BeCloseTo(10_000, 100);
        segment.Pieces[0].SongStartMilliseconds.Should().Be(0);
        segment.Pieces[0].DurationMilliseconds.Should().BeCloseTo(48_000, 100);
    }

    [Test]
    public void Align_UnknownDuration_EndsAtTheEndOfTheFile()
    {
        float[] video = SyntheticSound.Bursts(60, _lines.Select(line => line + 6));

        DetectedSegment? segment = LyricsAligner.Align(
            SyntheticSound.Features(video),
            [.. _lines.Select(TimeSpan.FromSeconds)],
            null);

        segment!.Pieces[0].FileStartMilliseconds.Should().BeCloseTo(6000, 100);
        (segment.Pieces[0].FileStartMilliseconds + segment.Pieces[0].DurationMilliseconds)
            .Should().BeCloseTo(60_000, 50);
    }

    [Test]
    public void Align_OtherVoice_IsNotConfident()
    {
        // Lines at other times than the onsets of the file: no shift fits clearly.
        Random random = new(5);
        float[] video = SyntheticSound.Bursts(65, Enumerable.Range(0, 30).Select(_ => random.NextDouble() * 60));

        DetectedSegment? segment = LyricsAligner.Align(
            SyntheticSound.Features(video),
            [.. _lines.Select(TimeSpan.FromSeconds)],
            TimeSpan.FromSeconds(48));

        (segment?.Confidence ?? 0).Should().BeLessThan(SongSegment.ConfidentFrom);
    }

    [Test]
    public void Align_FileOfTheLengthOfTheRecording_ReturnsNull()
    {
        float[] audio = SyntheticSound.Bursts(48, _lines);

        LyricsAligner.Align(
                SyntheticSound.Features(audio),
                [.. _lines.Select(TimeSpan.FromSeconds)],
                TimeSpan.FromSeconds(49))
            .Should().BeNull();
    }

    [Test]
    public void Align_TooFewLines_ReturnsNull()
    {
        float[] video = SyntheticSound.Bursts(60, _lines.Select(line => line + 10));

        LyricsAligner.Align(
                SyntheticSound.Features(video),
                [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(9)],
                null)
            .Should().BeNull();
    }
}
