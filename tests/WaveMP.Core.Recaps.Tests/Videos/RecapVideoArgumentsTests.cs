using AwesomeAssertions;
using WaveMP.Core.Recaps.Videos;

namespace WaveMP.Core.Recaps.Tests.Videos;

[TestFixture]
public class RecapVideoArgumentsTests
{
    [Test]
    public void Build_Always_EncodesH264AndAacIntoMp4WithTheIndexFirst()
    {
        IReadOnlyList<string> arguments = RecapVideoArguments.Build("in.webm", "out.mp4", "veryfast");

        string line = string.Join(' ', arguments);
        line.Should().StartWith("-hide_banner -nostdin -y -i in.webm");
        line.Should().Contain("-map 0:v:0 -map 0:a:0?");
        line.Should().Contain("-c:v libx264 -preset veryfast");
        line.Should().Contain("-pix_fmt yuv420p");
        line.Should().Contain("-c:a aac");
        line.Should().Contain("-movflags +faststart");
        line.Should().Contain("-r 30");
        arguments[^1].Should().Be("out.mp4");
        arguments[^2].Should().Be("mp4");
    }
}
