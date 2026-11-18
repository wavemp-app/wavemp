using AwesomeAssertions;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Core.Users.Tests.Settings;

[TestFixture]
public class ThemeIdsTests
{
    [TestCase("midnight")]
    [TestCase("clear-glass")]
    [TestCase("a")]
    [TestCase("theme2")]
    [TestCase("my-theme-2026")]
    [TestCase("6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    public void IsValid_WellFormedId_ReturnsTrue(string id)
    {
        ThemeIds.IsValid(id).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Midnight")]
    [TestCase("clear_glass")]
    [TestCase("clear glass")]
    [TestCase("-midnight")]
    [TestCase("midnight-")]
    [TestCase("clear--glass")]
    [TestCase("midnight\n")]
    [TestCase("../midnight")]
    [TestCase("mitternacht-ü")]
    public void IsValid_InvalidId_ReturnsFalse(string? id)
    {
        ThemeIds.IsValid(id).Should().BeFalse();
    }

    [Test]
    public void IsValid_IdUpToTheLongestLength_ReturnsTrue()
    {
        ThemeIds.IsValid(new string('a', ThemeIds.MaxLength)).Should().BeTrue();
        ThemeIds.IsValid(new string('a', ThemeIds.MaxLength + 1)).Should().BeFalse();
    }

    [Test]
    public void Default_IsMidnight()
    {
        ThemeIds.Default.Should().Be("midnight");
        ThemeIds.IsValid(ThemeIds.Default).Should().BeTrue();
    }
}
