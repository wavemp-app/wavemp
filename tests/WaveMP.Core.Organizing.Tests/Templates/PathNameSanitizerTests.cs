using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Organizing.Templates;

namespace WaveMP.Core.Organizing.Tests.Templates;

[TestFixture]
public class PathNameSanitizerTests
{
    [TestCase("AC/DC", "AC_DC")]
    [TestCase("Back\\Slash", "Back_Slash")]
    [TestCase("What? Why: \"Now\" <A|B> *", "What_ Why_ _Now_ _A_B")]
    [TestCase("Tab\there", "Tab here")]
    [TestCase("Bell\u0007", "Bell")]
    public void SanitizeName_InvalidCharacters_AreReplaced(string name, string expected)
    {
        PathNameSanitizer.SanitizeName(name).Should().Be(expected);
    }

    [TestCase("Album...", "Album")]
    [TestCase("Album . ", "Album")]
    [TestCase(" - Title - ", "Title")]
    [TestCase("_, Title", "Title")]
    [TestCase("  Many    spaces  ", "Many spaces")]
    public void SanitizeName_SeparatorsAndDotsAtTheEnds_AreRemoved(string name, string expected)
    {
        PathNameSanitizer.SanitizeName(name).Should().Be(expected);
    }

    [TestCase(".hidden", "_hidden")]
    [TestCase("...And Justice", "_..And Justice")]
    [TestCase("@eaDir", "_@eaDir")]
    [TestCase("#recycle", "_#recycle")]
    [TestCase("lost+found", "_lost+found")]
    [TestCase("System Volume Information", "_System Volume Information")]
    public void SanitizeName_NamesTheScanSkips_AreChanged(string name, string expected)
    {
        PathNameSanitizer.SanitizeName(name).Should().Be(expected);
    }

    [TestCase("CON", "CON_")]
    [TestCase("nul", "nul_")]
    [TestCase("COM1", "COM1_")]
    [TestCase("lpt9", "lpt9_")]
    [TestCase("AUX.flac", "AUX_.flac")]
    [TestCase("Prn .x", "Prn_.x")]
    [TestCase("CON. ", "CON_")]
    [TestCase("CONSOLE", "CONSOLE")]
    [TestCase("COM10", "COM10")]
    public void SanitizeName_ReservedWindowsNames_GetASuffix(string name, string expected)
    {
        PathNameSanitizer.SanitizeName(name).Should().Be(expected);
    }

    [TestCase("?")]
    [TestCase("...")]
    [TestCase(" - ")]
    [TestCase("")]
    public void SanitizeName_NothingLeft_IsEmpty(string name)
    {
        PathNameSanitizer.SanitizeName(name).Should().BeEmpty();
    }

    [Test]
    public void SanitizeName_DecomposedText_IsComposed()
    {
        PathNameSanitizer.SanitizeName("Beyoncé").Should().Be("Beyoncé");
    }

    [Test]
    public void SanitizeName_AnEmoji_IsKept()
    {
        PathNameSanitizer.SanitizeName("Song \U0001F3B5").Should().Be("Song \U0001F3B5");
    }

    [Test]
    public void SanitizeName_ALoneSurrogate_IsReplaced()
    {
        PathNameSanitizer.SanitizeName("A\uD83CB").Should().Be("A_B");
    }

    [Test]
    public void SanitizeName_ALongName_IsCutToTheByteLimitOnACharacterBoundary()
    {
        string name = string.Concat(Enumerable.Repeat("é\U0001F3B5", 100));

        string result = PathNameSanitizer.SanitizeName(name, reservedBytes: 5);

        Encoding.UTF8.GetByteCount(result).Should().BeLessThanOrEqualTo(PathNameSanitizer.MaxNameBytes - 5);
        result.Should().NotBeEmpty();
        name.Should().StartWith(result);
        char.IsHighSurrogate(result[^1]).Should().BeFalse();
    }

    [Test]
    public void SanitizeName_ALongNameCutBeforeADot_HasNoTrailingDot()
    {
        string name = new string('a', 254) + ". b";

        PathNameSanitizer.SanitizeName(name).Should().Be(new string('a', 254));
    }
}
