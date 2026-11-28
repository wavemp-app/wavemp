using AwesomeAssertions;
using WaveMP.Core.Analysis.Detection;
using WaveMP.Core.Analysis.Tests.Sounds;
using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Tests.Detection;

[TestFixture]
public class StructureDetectorTests
{
    [Test]
    public void Detect_IntroAndOutroSeparatedBySilence_FindsTheSongBetweenThem()
    {
        float[] video = SyntheticSound.Concat(
            SyntheticSound.Noise(5, 1),
            SyntheticSound.Silence(1),
            SyntheticSound.Melody(60, 2),
            SyntheticSound.Silence(1.5),
            SyntheticSound.Noise(4, 3));

        DetectedSegment? segment = StructureDetector.Detect(SyntheticSound.Features(video));

        segment.Should().NotBeNull();
        segment!.Method.Should().Be(SongSegmentMethod.Structure);
        segment.Confidence.Should().BeLessThan(SongSegment.ConfidentFrom);
        segment.Pieces.Should().ContainSingle();
        segment.Pieces[0].FileStartMilliseconds.Should().BeCloseTo(6000, 100);
        (segment.Pieces[0].FileStartMilliseconds + segment.Pieces[0].DurationMilliseconds)
            .Should().BeCloseTo(66_000, 100);
    }

    [Test]
    public void Detect_SilenceAtTheVeryStartAndEnd_IsPartOfTheSong()
    {
        float[] song = SyntheticSound.Concat(
            SyntheticSound.Silence(2),
            SyntheticSound.Melody(60, 4),
            SyntheticSound.Silence(3));

        StructureDetector.Detect(SyntheticSound.Features(song)).Should().BeNull();
    }

    [Test]
    public void Detect_PauseInTheMiddle_IsPartOfTheSong()
    {
        float[] song = SyntheticSound.Concat(
            SyntheticSound.Melody(40, 5),
            SyntheticSound.Silence(2),
            SyntheticSound.Melody(40, 6));

        StructureDetector.Detect(SyntheticSound.Features(song)).Should().BeNull();
    }

    [Test]
    public void Detect_ShortFile_ReturnsNull()
    {
        StructureDetector.Detect(SyntheticSound.Features(SyntheticSound.Melody(5, 7))).Should().BeNull();
    }
}
