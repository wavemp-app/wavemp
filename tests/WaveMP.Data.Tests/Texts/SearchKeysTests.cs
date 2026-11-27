using AwesomeAssertions;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Tests.Texts;

[TestFixture]
public class SearchKeysTests
{
    [TestCase("P!nk", "pink")]
    [TestCase("Ke$ha", "kesha")]
    [TestCase("$uicideboy$", "suicideboys")]
    [TestCase("A$AP Rocky", "asaprocky")]
    [TestCase("Th@ Band", "thaband")]
    [TestCase("G0rillaz", "gorillaz")]
    [TestCase("B3ck", "beck")]
    [TestCase("Simon & Garfunkel", "simonandgarfunkel")]
    [TestCase("Simon&Garfunkel", "simonandgarfunkel")]
    [TestCase("Florence + the Machine", "florenceandthemachine")]
    [TestCase("Rock'n'Roll", "rocknroll")]
    public void Variant_SymbolsThatStandForLetters_BecomeTheseLetters(string text, string expected)
    {
        SearchKeys.Variant(text).Should().Be(expected);
    }

    [TestCase("3 Doors Down", "3doorsdown")]
    [TestCase("50 Cent", "50cent")]
    [TestCase("2003", "2003")]
    [TestCase("Wham!", "wham")]
    [TestCase("Panic! at the Disco", "panicatthedisco")]
    [TestCase("!!!", "!!!")]
    public void Variant_DigitsWithoutLettersAndExclamationMarks_StayAsTheyAre(string text, string expected)
    {
        SearchKeys.Variant(text).Should().Be(expected);
    }

    [TestCase("P!nk", "pnk pink")]
    [TestCase("Ke$ha", "keha kesha")]
    [TestCase("Simon & Garfunkel", "simongarfunkel simonandgarfunkel")]
    [TestCase("Rihanna", "rihanna")]
    [TestCase("K.Flay", "kflay")]
    [TestCase("Beyoncé", "beyonce")]
    public void Of_Name_IsTheNormalizedNameAndItsDifferentVariant(string name, string expected)
    {
        SearchKeys.Of(name).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void OfAndVariant_NoContent_AreNull(string? name)
    {
        SearchKeys.Of(name).Should().BeNull();
        SearchKeys.Variant(name).Should().BeNull();
    }

    [Test]
    public void Of_BothDirections_ShareTheVariant()
    {
        // "pink" finds "P!nk" and "p!nk" finds "Pink": the variant of the text is part of the key of the name.
        SearchKeys.Of("P!nk")!.Split(' ').Should().Contain(SearchKeys.Variant("pink"));
        SearchKeys.Of("Pink")!.Split(' ').Should().Contain(SearchKeys.Variant("p!nk"));
    }

    [Test]
    public void Of_LongName_StaysWithinTheMaximum()
    {
        string name = string.Concat(Enumerable.Repeat("P!nk & ", 200));

        SearchKeys.Of(name)!.Length.Should().BeLessThanOrEqualTo(SearchKeys.MaxLength);
    }
}
