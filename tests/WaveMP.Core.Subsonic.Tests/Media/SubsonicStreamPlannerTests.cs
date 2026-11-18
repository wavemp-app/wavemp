using AwesomeAssertions;
using WaveMP.Core.Subsonic.Media;

namespace WaveMP.Core.Subsonic.Tests.Media;

[TestFixture]
public class SubsonicStreamPlannerTests
{
    private const int DefaultBitrate = 256;

    [TestCase("mp3")]
    [TestCase("flac")]
    [TestCase("m4a")]
    [TestCase("opus")]
    public void Plan_CommonFormatWithoutWishes_PlaysTheFileDirectly(string extension)
    {
        Plan(null, null, extension, 900_000).Should().Be(SubsonicStreamPlan.Direct);
    }

    [Test]
    public void Plan_UncommonFormat_TranscodesToMp3WithTheDefaultBitrate()
    {
        SubsonicStreamPlan plan = Plan(null, null, "wma", 192_000);

        plan.Should().Be(new SubsonicStreamPlan(true, SubsonicTranscodeFormat.Mp3, DefaultBitrate, 0));
    }

    [Test]
    public void Plan_FileAboveTheMaxBitRate_TranscodesWithTheMaxBitRate()
    {
        SubsonicStreamPlan plan = Plan(null, 128, "flac", 900_000);

        plan.Should().Be(new SubsonicStreamPlan(true, SubsonicTranscodeFormat.Mp3, 128, 0));
    }

    [Test]
    public void Plan_FileWithinTheMaxBitRate_PlaysDirectly()
    {
        Plan(null, 320, "mp3", 256_000).Should().Be(SubsonicStreamPlan.Direct);
    }

    [Test]
    public void Plan_RequestedFormatOfTheFile_PlaysDirectly()
    {
        Plan("mp3", null, "mp3", 320_000).Should().Be(SubsonicStreamPlan.Direct);
    }

    [TestCase("opus", SubsonicTranscodeFormat.Opus)]
    [TestCase("ogg", SubsonicTranscodeFormat.Opus)]
    [TestCase("aac", SubsonicTranscodeFormat.Aac)]
    [TestCase("mp3", SubsonicTranscodeFormat.Mp3)]
    public void Plan_OtherRequestedFormat_TranscodesToIt(string format, SubsonicTranscodeFormat expected)
    {
        SubsonicStreamPlan plan = Plan(format, null, "flac", 900_000);

        plan.Transcode.Should().BeTrue();
        plan.Format.Should().Be(expected);
    }

    [Test]
    public void Plan_Raw_PlaysDirectlyDespiteTheMaxBitRate()
    {
        Plan("raw", 64, "flac", 900_000).Should().Be(SubsonicStreamPlan.Direct);
    }

    [Test]
    public void Plan_SoundOfAVideo_IsAlwaysTranscoded()
    {
        SubsonicStreamPlan plan = SubsonicStreamPlanner.Plan("raw", null, null, "mkv", null, true, DefaultBitrate);

        plan.Transcode.Should().BeTrue();
        plan.Format.Should().Be(SubsonicTranscodeFormat.Mp3);
    }

    [TestCase(8, 32)]
    [TestCase(1000, 320)]
    public void Plan_MaxBitRateOutsideTheLimits_IsClamped(int maxBitRate, int expected)
    {
        Plan(null, maxBitRate, "flac", 5_000_000).BitrateKbps.Should().Be(expected);
    }

    [Test]
    public void Plan_TimeOffset_StartsTheTranscodeThere()
    {
        SubsonicStreamPlanner.Plan("mp3", null, 42, "flac", 900_000, false, DefaultBitrate)
            .OffsetSeconds.Should().Be(42);
    }

    private static SubsonicStreamPlan Plan(string? format, int? maxBitRate, string extension, int? fileBitrate)
    {
        return SubsonicStreamPlanner.Plan(format, maxBitRate, null, extension, fileBitrate, false, DefaultBitrate);
    }
}
