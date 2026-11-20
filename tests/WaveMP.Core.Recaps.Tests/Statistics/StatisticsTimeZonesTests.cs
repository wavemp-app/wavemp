using AwesomeAssertions;
using WaveMP.Core.Recaps.Statistics;

namespace WaveMP.Core.Recaps.Tests.Statistics;

[TestFixture]
public class StatisticsTimeZonesTests
{
    [TestCase("UTC")]
    [TestCase("Europe/Berlin")]
    [TestCase("America/New_York")]
    public void TryFind_IanaId_FindsTheTimeZone(string id)
    {
        StatisticsTimeZones.TryFind(id, out TimeZoneInfo zone).Should().BeTrue();
        zone.Should().NotBeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Mars/Olympus_Mons")]
    public void TryFind_UnknownId_ReturnsFalse(string? id)
    {
        StatisticsTimeZones.TryFind(id, out _).Should().BeFalse();
    }

    [Test]
    public void TryFind_TooLongId_ReturnsFalse()
    {
        StatisticsTimeZones.TryFind(new string('a', StatisticsTimeZones.MaxLength + 1), out _).Should().BeFalse();
    }

    [Test]
    public void StartOfDay_Always_ReturnsMidnightInTheTimeZone()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        DateTimeOffset winter = StatisticsTimeZones.StartOfDay(new DateOnly(2026, 11, 1), berlin);
        DateTimeOffset summer = StatisticsTimeZones.StartOfDay(new DateOnly(2026, 7, 1), berlin);

        winter.Should().Be(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero));
        summer.Should().Be(new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void DayOf_Always_ReturnsTheDayInTheTimeZone()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        DateTimeOffset moment = new(2026, 11, 9, 23, 30, 0, TimeSpan.Zero);

        StatisticsTimeZones.DayOf(moment, berlin).Should().Be(new DateOnly(2026, 11, 10));
        StatisticsTimeZones.DayOf(moment, TimeZoneInfo.Utc).Should().Be(new DateOnly(2026, 11, 9));
    }
}
