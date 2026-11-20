using AwesomeAssertions;
using WaveMP.Core.Caching.Covers;

namespace WaveMP.Core.Caching.Tests.Covers;

[TestFixture]
public class CoverSizesTests
{
    [TestCase(1, 96)]
    [TestCase(96, 96)]
    [TestCase(97, 192)]
    [TestCase(192, 192)]
    [TestCase(193, 300)]
    [TestCase(300, 300)]
    [TestCase(301, 600)]
    [TestCase(600, 600)]
    [TestCase(601, 1000)]
    [TestCase(1000, 1000)]
    [TestCase(1001, 2000)]
    [TestCase(2000, 2000)]
    [TestCase(4096, 2000)]
    public void SnapScaled_RequestedSize_RoundsUpToAScaledSize(int requested, int expected)
    {
        CoverSizes.SnapScaled(requested).Should().Be(expected);
    }

    [TestCase(1, 96)]
    [TestCase(96, 96)]
    [TestCase(97, 300)]
    [TestCase(192, 300)]
    [TestCase(300, 300)]
    [TestCase(301, 1000)]
    [TestCase(600, 1000)]
    [TestCase(1000, 1000)]
    [TestCase(2000, 1000)]
    [TestCase(4096, 1000)]
    public void SnapStored_RequestedSize_RoundsUpToAStoredSize(int requested, int expected)
    {
        CoverSizes.SnapStored(requested).Should().Be(expected);
    }

    // A phone with a pixel density of 3 shows the full-screen player about 400 pixels wide (1200 pixels), a 4K screen
    // with a pixel density of 2 the stage of Now Playing about 288 to 900 pixels wide (576 to 1800 pixels).
    [TestCase(44 * 3)]
    [TestCase(176 * 3)]
    [TestCase(400 * 3)]
    [TestCase(900 * 2)]
    public void SnapScaled_ShownSizeOfDenseScreens_IsNeverSmallerThanTheShownPixels(int pixels)
    {
        CoverSizes.SnapScaled(pixels).Should().BeGreaterThanOrEqualTo(pixels);
    }

    [Test]
    public void Sizes_Always_PrepareTheSmallerOnesOfTheScaledSizes()
    {
        CoverSizes.Scaled.Should().Equal(96, 192, 300, 600, 1000, 2000);
        CoverSizes.Scaled.Should().BeInAscendingOrder();
        CoverSizes.Stored.Should().Equal(96, 300, 1000);
        CoverSizes.Scaled.Should().Contain(CoverSizes.Stored);
        CoverSizes.Prepared.Should().Equal(96, 300);
        CoverSizes.Scaled.Should().Contain(CoverSizes.Prepared);
    }
}
