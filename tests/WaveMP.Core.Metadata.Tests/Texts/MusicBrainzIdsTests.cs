using AwesomeAssertions;
using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Metadata.Tests.Texts;

[TestFixture]
public class MusicBrainzIdsTests
{
    private const string First = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";
    private const string Second = "a74b1b7f-71a5-4011-9441-d0b5e4122711";

    [TestCase(First)]
    [TestCase(" 5B11F4CE-A62D-471E-81FC-A69A8278C7DA ")]
    [TestCase(First + "/")]
    public void Single_OneId_ReturnsIt(string text)
    {
        MusicBrainzIds.Single(text).Should().Be(Guid.Parse(First));
        MusicBrainzIds.SingleText(text).Should().Be(First);
    }

    [TestCase(First + "/" + Second)]
    [TestCase(First + "; " + Second)]
    [TestCase(First + "\0" + Second)]
    [TestCase(First + " " + Second)]
    public void Single_SeveralIds_ReturnsNone(string text)
    {
        MusicBrainzIds.Single(text).Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not an id")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public void Single_NoId_ReturnsNone(string? text)
    {
        MusicBrainzIds.Single(text).Should().BeNull();
        MusicBrainzIds.SingleText(text).Should().BeNull();
    }
}
