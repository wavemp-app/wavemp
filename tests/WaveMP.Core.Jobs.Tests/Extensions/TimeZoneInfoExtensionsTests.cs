using AwesomeAssertions;
using WaveMP.Core.Jobs.Extensions;

namespace WaveMP.Core.Jobs.Tests.Extensions;

[TestFixture]
public class TimeZoneInfoExtensionsTests
{
    [TestCase("Europe/Berlin")]
    [TestCase("America/New_York")]
    [TestCase("UTC")]
    public void ToIanaId_ZoneWithIanaId_ReturnsItsId(string id)
    {
        TimeZoneInfo.FindSystemTimeZoneById(id).ToIanaId().Should().Be(id);
    }

    [TestCase("W. Europe Standard Time", "Europe/Berlin")]
    [TestCase("Eastern Standard Time", "America/New_York")]
    [TestCase("Tokyo Standard Time", "Asia/Tokyo")]
    public void ToIanaId_ZoneWithWindowsId_ReturnsTheIanaIdOfTheZone(string windowsId, string expected)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);

        timeZone.ToIanaId().Should().Be(expected);
    }

    [Test]
    public void ToIanaId_CustomZone_ReturnsNull()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("WaveMP Test", TimeSpan.FromHours(3), "Test", "Test");

        timeZone.ToIanaId().Should().BeNull();
    }

    [Test]
    public void ToIanaId_LocalZone_ReturnsAnIanaIdOnTheServer()
    {
        // Linux containers and Windows development machines both know the IANA id of their time zone.
        TimeZoneInfo.Local.ToIanaId().Should().NotBeNullOrWhiteSpace();
    }
}
