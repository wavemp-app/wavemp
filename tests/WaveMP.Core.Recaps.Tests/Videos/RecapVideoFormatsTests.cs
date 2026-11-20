using AwesomeAssertions;
using WaveMP.Core.Recaps.Videos;

namespace WaveMP.Core.Recaps.Tests.Videos;

[TestFixture]
public class RecapVideoFormatsTests
{
    [Test]
    public void IsSupported_EbmlHeader_ReturnsTrue()
    {
        RecapVideoFormats.IsSupported([0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81]).Should().BeTrue();
    }

    [Test]
    public void IsSupported_Mp4FileTypeBox_ReturnsTrue()
    {
        RecapVideoFormats.IsSupported([0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i'])
            .Should().BeTrue();
    }

    [Test]
    public void IsSupported_OtherContent_ReturnsFalse()
    {
        RecapVideoFormats.IsSupported("GIF89a\0\0\0\0\0\0"u8).Should().BeFalse();
        RecapVideoFormats.IsSupported([]).Should().BeFalse();
        RecapVideoFormats.IsSupported([0x1A, 0x45]).Should().BeFalse();
    }
}
