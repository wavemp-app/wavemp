using AwesomeAssertions;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Tests.Settings;

[TestFixture]
public class VisualizationSettingsLimitsTests
{
    [TestCase(10)]
    [TestCase(25)]
    [TestCase(50)]
    [TestCase(100)]
    public void IsValidMilkdropSpeed_WithinTheRange_ReturnsTrue(int percent)
    {
        VisualizationSettingsLimits.IsValidMilkdropSpeed(percent).Should().BeTrue();
    }

    [TestCase(9)]
    [TestCase(0)]
    [TestCase(-50)]
    [TestCase(101)]
    public void IsValidMilkdropSpeed_OutsideTheRange_ReturnsFalse(int percent)
    {
        VisualizationSettingsLimits.IsValidMilkdropSpeed(percent).Should().BeFalse();
    }

    [Test]
    public void MilkdropSpeedDefault_IsWithinTheRangeAndTheDefaultOfNewProfiles()
    {
        VisualizationSettingsLimits.IsValidMilkdropSpeed(VisualizationSettingsLimits.MilkdropSpeedDefault)
            .Should().BeTrue();
        VisualizationSettingsLimits.MilkdropSpeedDefault.Should().Be(50);
        new UserSettings().VisualizationMilkdropSpeed.Should().Be(VisualizationSettingsLimits.MilkdropSpeedDefault);
    }

    [Test]
    public void IsValidColorScheme_UpToTheLongestLength_ReturnsTrue()
    {
        VisualizationSettingsLimits.IsValidColorScheme(string.Empty).Should().BeTrue();
        VisualizationSettingsLimits.IsValidColorScheme(
            new string('a', VisualizationSettingsLimits.ColorSchemeMaxLength))
            .Should().BeTrue();
        VisualizationSettingsLimits
            .IsValidColorScheme(new string('a', VisualizationSettingsLimits.ColorSchemeMaxLength + 1))
            .Should().BeFalse();
    }
}
