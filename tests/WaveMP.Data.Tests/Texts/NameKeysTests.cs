using AwesomeAssertions;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Tests.Texts;

[TestFixture]
public class NameKeysTests
{
    [TestCase("K.Flay", "kflay")]
    [TestCase("K/DA", "kda")]
    [TestCase("Beyoncé", "beyonce")]
    [TestCase("BEYONCÉ", "beyonce")]
    [TestCase("AC/DC", "acdc")]
    [TestCase("ac dc", "acdc")]
    [TestCase("Guns N' Roses", "gunsnroses")]
    [TestCase("Simon & Garfunkel", "simongarfunkel")]
    [TestCase("  Sigur Rós ", "sigurros")]
    [TestCase("Mötley Crüe", "motleycrue")]
    [TestCase("Hip-Hop", "hiphop")]
    [TestCase("blink-182", "blink182")]
    [TestCase("Ⅻ", "ⅻ")]
    public void Of_Name_IsLowerCaseWithoutDiacriticsAndPunctuation(string name, string expected)
    {
        NameKeys.Of(name).Should().Be(expected);
    }

    [TestCase("坂本龍一", "坂本龍一")]
    [TestCase("Мумий Тролль", "мумиитролль")]
    [TestCase("방탄소년단", "방탄소년단")]
    [TestCase("Ελένη", "ελενη")]
    [TestCase("ガガ", "ガガ")]
    [TestCase("हिन्दी", "हिन्दी")]
    public void Of_NameInAnotherScript_KeepsItsLetters(string name, string expected)
    {
        NameKeys.Of(name).Should().Be(expected);
    }

    [TestCase("!!!", "!!!")]
    [TestCase(" ÷ ", "÷")]
    [TestCase("? \t ?", "? ?")]
    public void Of_NameWithoutLettersAndDigits_KeepsItsCharacters(string name, string expected)
    {
        NameKeys.Of(name).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Of_NoName_IsNull(string? name)
    {
        NameKeys.Of(name).Should().BeNull();
    }

    [Test]
    public void Of_VeryLongName_IsCutWithoutSplittingASurrogatePair()
    {
        string name = new string('a', NameKeys.MaxLength - 1) + "\U0001D400\U0001D400";

        string key = NameKeys.Of(name)!;

        key.Length.Should().Be(NameKeys.MaxLength - 1);
        key.Should().Be(new string('a', NameKeys.MaxLength - 1));
    }

    [TestCase("K.Flay", "kflay", true)]
    [TestCase("AC/DC", "AC-DC", true)]
    [TestCase("The Band", "Band", false)]
    [TestCase(null, " ", true)]
    [TestCase("Ø", "O", false)]
    public void AreEqual_TwoNames_ComparesTheirKeys(string? left, string? right, bool expected)
    {
        NameKeys.AreEqual(left, right).Should().Be(expected);
    }
}
