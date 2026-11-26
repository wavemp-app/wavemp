using AwesomeAssertions;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Transcoding.Arguments;

namespace WaveMP.Core.Transcoding.Tests.Arguments;

[TestFixture]
public class FfmpegRemuxArgumentsTests
{
    [Test]
    public void Build_CopiesThePictureIntoMp4WithTheIndexAtTheStart()
    {
        IReadOnlyList<string> arguments = FfmpegRemuxArguments.Build(
            "/media/Song.mkv",
            VideoCodec.H264,
            RemuxAudio.Copy,
            "/cache/tmp/out.mp4");

        arguments.Should().ContainInConsecutiveOrder("-i", "/media/Song.mkv");
        arguments.Should().ContainInConsecutiveOrder("-map", "0:v:0", "-c:v", "copy");
        arguments.Should().ContainInConsecutiveOrder("-map", "0:a:0", "-c:a", "copy");
        arguments.Should().ContainInConsecutiveOrder("-movflags", "+faststart");
        arguments.Should().ContainInConsecutiveOrder("-f", "mp4", "/cache/tmp/out.mp4");
        arguments.Should().NotContain("-tag:v");
        arguments[^1].Should().Be("/cache/tmp/out.mp4");
    }

    [Test]
    public void Build_SoundTheClientDoesNotPlay_ConvertsItToAac()
    {
        IReadOnlyList<string> arguments = FfmpegRemuxArguments.Build(
            "/a.mkv",
            VideoCodec.H264,
            RemuxAudio.Aac,
            "/b.mp4");

        arguments.Should().ContainInConsecutiveOrder("-map", "0:a:0", "-c:a", "aac", "-b:a", "192k", "-ac", "2");
        arguments.Should().ContainInConsecutiveOrder("-c:v", "copy");
    }

    [Test]
    public void Build_VideoWithoutSound_LeavesTheSoundOut()
    {
        IReadOnlyList<string> arguments = FfmpegRemuxArguments.Build(
            "/a.mov",
            VideoCodec.H264,
            RemuxAudio.None,
            "/b.mp4");

        arguments.Should().Contain("-an");
        arguments.Should().NotContain("0:a:0");
    }

    [Test]
    public void Build_Hevc_TagsItForSafari()
    {
        IReadOnlyList<string> arguments = FfmpegRemuxArguments.Build(
            "/a.mkv",
            VideoCodec.Hevc,
            RemuxAudio.Copy,
            "/b.mp4");

        arguments.Should().ContainInConsecutiveOrder("-tag:v", "hvc1");
    }
}
