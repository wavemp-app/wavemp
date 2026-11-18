using AwesomeAssertions;
using WaveMP.Core.Subsonic.Media;

namespace WaveMP.Core.Subsonic.Tests.Media;

[TestFixture]
public class SubsonicTranscodeArgumentsTests
{
    [Test]
    public void Build_Mp3_WritesTheSoundAsMp3ToTheOutput()
    {
        IReadOnlyList<string> arguments = SubsonicTranscodeArguments.Build(
            "/media/Song.flac",
            new SubsonicStreamPlan(true, SubsonicTranscodeFormat.Mp3, 192, 0));

        arguments.Should().Equal(
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-i", "/media/Song.flac", "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1",
            "-ac", "2", "-c:a", "libmp3lame", "-b:a", "192k", "-f", "mp3", "pipe:1");
    }

    [Test]
    public void Build_Offset_SeeksBeforeTheInput()
    {
        IReadOnlyList<string> arguments = SubsonicTranscodeArguments.Build(
            "/media/Song.flac",
            new SubsonicStreamPlan(true, SubsonicTranscodeFormat.Opus, 128, 90));

        arguments.Take(6).Should().Equal("-nostdin", "-hide_banner", "-loglevel", "error", "-ss", "90");
        arguments.Should().ContainInConsecutiveOrder("-c:a", "libopus", "-b:a", "128k", "-f", "ogg");
    }

    [Test]
    public void Build_Aac_WritesAdts()
    {
        IReadOnlyList<string> arguments = SubsonicTranscodeArguments.Build(
            "/media/Video.mkv",
            new SubsonicStreamPlan(true, SubsonicTranscodeFormat.Aac, 256, 0));

        arguments.Should().ContainInConsecutiveOrder("-c:a", "aac", "-b:a", "256k", "-f", "adts", "pipe:1");
    }
}
