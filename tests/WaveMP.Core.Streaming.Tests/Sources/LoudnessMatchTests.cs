using AwesomeAssertions;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Streaming.Tests.Sources;

[TestFixture]
public class LoudnessMatchTests
{
    [TestCase(-14.0, -8.5, -5.5)]
    [TestCase(-9.0, -12.04, 3.0)]
    [TestCase(-10.0, -40.0, 12.0)]
    [TestCase(-40.0, -10.0, -12.0)]
    public void Between_BothMeasured_IsTheDifferenceWithinTheLimit(double reference, double source, double expected)
    {
        LoudnessMatch.Between(reference, source).Should().Be(expected);
    }

    [TestCase(null, -8.0)]
    [TestCase(-8.0, null)]
    public void Between_OneUnknown_IsNull(double? reference, double? source)
    {
        LoudnessMatch.Between(reference, source).Should().BeNull();
    }
}
