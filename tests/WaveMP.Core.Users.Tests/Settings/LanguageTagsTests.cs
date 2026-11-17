using AwesomeAssertions;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Core.Users.Tests.Settings;

[TestFixture]
public class LanguageTagsTests
{
    [TestCase("en")]
    [TestCase("de")]
    [TestCase("fil")]
    [TestCase("pt-BR")]
    [TestCase("pt-br")]
    [TestCase("zh-Hans")]
    [TestCase("zh-Hant-TW")]
    [TestCase("es-419")]
    [TestCase("sr-Latn-RS")]
    [TestCase("de-CH-1996")]
    [TestCase("ca-ES-valencia")]
    public void IsValid_WellFormedTag_ReturnsTrue(string tag)
    {
        LanguageTags.IsValid(tag).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("e")]
    [TestCase("english")]
    [TestCase("de_DE")]
    [TestCase("de-")]
    [TestCase("-de")]
    [TestCase("de DE")]
    [TestCase("de\n")]
    [TestCase("de-DE-x-private")]
    [TestCase("de-u-co-phonebk")]
    [TestCase("de-abcdefgh-abcdefgh-abcdefgh-abcdefgh")]
    public void IsValid_InvalidTag_ReturnsFalse(string? tag)
    {
        LanguageTags.IsValid(tag).Should().BeFalse();
    }

    [TestCase("en", "en")]
    [TestCase("DE", "de")]
    [TestCase("PT-br", "pt-BR")]
    [TestCase("zh-hans", "zh-Hans")]
    [TestCase("ZH-HANT-tw", "zh-Hant-TW")]
    [TestCase("es-419", "es-419")]
    [TestCase("DE-ch-1996", "de-CH-1996")]
    [TestCase("ca-es-VALENCIA", "ca-ES-valencia")]
    public void Normalize_ValidTag_ReturnsItInTheUsualCase(string tag, string expected)
    {
        LanguageTags.Normalize(tag).Should().Be(expected);
    }

    [Test]
    public void Normalize_InvalidTag_Throws()
    {
        Action act = () => LanguageTags.Normalize("de_DE");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Default_IsEnglish()
    {
        LanguageTags.Default.Should().Be("en");
    }
}
