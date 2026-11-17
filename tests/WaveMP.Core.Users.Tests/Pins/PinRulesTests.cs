using System.Text.RegularExpressions;
using AwesomeAssertions;
using WaveMP.Core.Users.Pins;

namespace WaveMP.Core.Users.Tests.Pins;

[TestFixture]
public class PinRulesTests
{
    [TestCase("1234")]
    [TestCase("0000")]
    [TestCase("12345678")]
    [TestCase("905172")]
    public void IsValid_FourToEightDigits_IsTrue(string pin)
    {
        PinRules.IsValid(pin).Should().BeTrue();
        Regex.IsMatch(pin, PinRules.Pattern).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("123")]
    [TestCase("123456789")]
    [TestCase("12a4")]
    [TestCase(" 1234")]
    [TestCase("1234 ")]
    [TestCase("12-34")]
    [TestCase("١٢٣٤")]
    public void IsValid_AnythingElse_IsFalse(string? pin)
    {
        PinRules.IsValid(pin).Should().BeFalse();
        if (pin is not null)
        {
            Regex.IsMatch(pin, PinRules.Pattern).Should().BeFalse();
        }
    }
}
