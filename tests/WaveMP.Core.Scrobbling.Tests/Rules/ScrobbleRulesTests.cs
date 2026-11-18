using AwesomeAssertions;
using WaveMP.Core.Scrobbling.Rules;

namespace WaveMP.Core.Scrobbling.Tests.Rules;

[TestFixture]
public class ScrobbleRulesTests
{
    [TestCase(29.9, false)]
    [TestCase(30, true)]
    [TestCase(240, true)]
    public void IsEligible_Duration_ReportsSongsOfAtLeastThirtySeconds(double seconds, bool expected)
    {
        ScrobbleRules.IsEligible(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Test]
    public void IsEligible_UnknownDuration_ReportsTheSong()
    {
        ScrobbleRules.IsEligible(null).Should().BeTrue();
    }

    [TestCase(60, 30)]
    [TestCase(300, 150)]
    [TestCase(480, 240)]
    [TestCase(3600, 240)]
    public void Threshold_Duration_IsHalfTheSongOrFourMinutes(double seconds, double expected)
    {
        ScrobbleRules.Threshold(TimeSpan.FromSeconds(seconds)).Should().Be(TimeSpan.FromSeconds(expected));
    }

    [Test]
    public void Threshold_UnknownDuration_IsFourMinutes()
    {
        ScrobbleRules.Threshold(null).Should().Be(TimeSpan.FromMinutes(4));
        ScrobbleRules.Threshold(TimeSpan.Zero).Should().Be(TimeSpan.FromMinutes(4));
    }

    [TestCase(200, 99, false)]
    [TestCase(200, 100, true)]
    [TestCase(600, 239, false)]
    [TestCase(600, 240, true)]
    [TestCase(20, 20, false)]
    public void IsScrobble_PlayedTime_CountsFromTheThreshold(double duration, double played, bool expected)
    {
        ScrobbleRules.IsScrobble(TimeSpan.FromSeconds(duration), TimeSpan.FromSeconds(played)).Should().Be(expected);
    }

    [Test]
    public void IsExpired_PlayOlderThanTheMaximumAge_IsExpired()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var maxAge = TimeSpan.FromDays(14);

        ScrobbleRules.IsExpired(now.AddDays(-14).AddSeconds(-1), now, maxAge).Should().BeTrue();
        ScrobbleRules.IsExpired(now.AddDays(-14), now, maxAge).Should().BeFalse();
        ScrobbleRules.IsExpired(now.AddDays(-1), now, maxAge).Should().BeFalse();
    }
}
