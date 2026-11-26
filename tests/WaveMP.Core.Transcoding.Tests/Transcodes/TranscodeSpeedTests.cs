using AwesomeAssertions;
using WaveMP.Core.Transcoding.Profiles;
using WaveMP.Core.Transcoding.Tests.Fakes;
using WaveMP.Core.Transcoding.Transcodes;

namespace WaveMP.Core.Transcoding.Tests.Transcodes;

[TestFixture]
public class TranscodeSpeedTests
{
    [Test]
    public void ProducedOf_Segments_AddsTheirDurations()
    {
        // 31 seconds in segments of 2 seconds: the last segment holds the rest of 3 seconds.
        TranscodeVariant variant = TestVariants.Video(TestVariants.Input(TimeSpan.FromSeconds(31)));

        TranscodeSpeed.ProducedOf(variant, 0, 3).Should().Be(TimeSpan.FromSeconds(6));
        TranscodeSpeed.ProducedOf(variant, 4, 4).Should().Be(TimeSpan.Zero);
        TranscodeSpeed.ProducedOf(variant, 13, 99).Should().Be(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Of_ProducedAndElapsed_IsTheSpeedFactor()
    {
        TranscodeSpeed.Of(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(4)).Should().Be(3);
        TranscodeSpeed.Of(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)).Should().Be(0.67);
    }

    [Test]
    public void Of_NothingProducedOrNoTime_IsUnknown()
    {
        TranscodeSpeed.Of(TimeSpan.Zero, TimeSpan.FromSeconds(4)).Should().BeNull();
        TranscodeSpeed.Of(TimeSpan.FromSeconds(4), TimeSpan.Zero).Should().BeNull();
    }

    [TestCase(0.8, 2, true)]
    [TestCase(0.8, 1, false)]
    [TestCase(1.0, 5, false)]
    [TestCase(null, 5, false)]
    public void IsSlowerThanRealTime_MeasuredAfterTwoSegments(double? speed, int segments, bool slower)
    {
        TranscodeSpeed.IsSlowerThanRealTime(speed, segments).Should().Be(slower);
    }
}
