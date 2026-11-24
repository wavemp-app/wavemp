using AwesomeAssertions;
using WaveMP.Core.Library.Groups;

namespace WaveMP.Core.Library.Tests.Groups;

/// <summary>
/// The web app files texts under the same letters; its tests use the same table of cases.
/// </summary>
[TestFixture]
public class IndexLettersTests
{
    [TestCase("Abba", "A")]
    [TestCase("abba", "A")]
    [TestCase("zappa", "Z")]
    [TestCase("\u00c9lan", "E")]
    [TestCase("E\u0301lan", "E")]
    [TestCase("\u00e4rzte", "A")]
    [TestCase("\u00d6sterreich", "O")]
    [TestCase("\u00d1and\u00fa", "N")]
    [TestCase("\u00c7a", "C")]
    [TestCase("\u00c5ngstr\u00f6m", "A")]
    [TestCase("\u0130stanbul", "I")]
    [TestCase("\u0131", "#")]
    [TestCase("\u00df", "#")]
    [TestCase("\u00d8resund", "#")]
    [TestCase("\u00c6on", "#")]
    [TestCase("\u0152uvre", "#")]
    [TestCase("\ufb01ve", "#")]
    [TestCase("\uff21", "#")]
    [TestCase("2Pac", "#")]
    [TestCase("(What's the Story)", "#")]
    [TestCase("!!!", "#")]
    [TestCase(" Space", "#")]
    [TestCase("\u03a9mega", "#")]
    [TestCase("\u0427\u0430\u0439\u0444", "#")]
    [TestCase("\u6771\u4eac", "#")]
    [TestCase("\ud83d\ude00", "#")]
    [TestCase("\u0301", "#")]
    [TestCase("\ud800", "#")]
    [TestCase("", "#")]
    [TestCase(null, "#")]
    public void Of_Text_IsFiledUnderTheLetterOfItsFirstCharacter(string? text, string expected)
    {
        IndexLetters.Of(text).Should().Be(expected);
    }
}
