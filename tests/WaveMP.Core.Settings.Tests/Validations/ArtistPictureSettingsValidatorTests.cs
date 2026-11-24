using AwesomeAssertions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Validations;

[TestFixture]
public class ArtistPictureSettingsValidatorTests
{
    [Test]
    public void Defaults_Always_AreOffWithoutApiKeyAndValid()
    {
        ArtistPictureSettings defaults = new();

        defaults.OnlineLookupEnabled.Should().BeFalse();
        defaults.FanartTvApiKey.Should().BeNull();
        ArtistPictureSettingsValidator.Validate(ArtistPictureSettingsValidator.Normalize(defaults)).Should().BeEmpty();
        ArtistPictureSettingsValidator.Normalize(defaults).Should().BeEquivalentTo(defaults);
    }

    [TestCase(" abc123DEF ", "abc123DEF")]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase(null, null)]
    public void Normalize_ApiKey_IsTrimmedAndEmptyMeansNone(string? key, string? expected)
    {
        ArtistPictureSettings normalized = ArtistPictureSettingsValidator.Normalize(
            new ArtistPictureSettings { OnlineLookupEnabled = true, FanartTvApiKey = key });

        normalized.FanartTvApiKey.Should().Be(expected);
        normalized.OnlineLookupEnabled.Should().BeTrue();
    }

    [TestCase("a")]
    [TestCase("0123456789abcdef0123456789ABCDEF")]
    [TestCase("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij1234")]
    public void Validate_ApiKeyOfLettersAndDigits_IsValid(string key)
    {
        ArtistPictureSettingsValidator.Validate(new ArtistPictureSettings { FanartTvApiKey = key })
            .Should().BeEmpty();
    }

    [TestCase("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij12345")]
    [TestCase("key with space")]
    [TestCase("key\r\nX-Header: 1")]
    [TestCase("schlüssel")]
    [TestCase("key-1")]
    public void Validate_ApiKeyWithOtherCharactersOrTooLong_IsInvalid(string key)
    {
        IReadOnlyList<ArtistPictureSettingsValidationError> errors =
            ArtistPictureSettingsValidator.Validate(new ArtistPictureSettings { FanartTvApiKey = key });

        errors.Should().Equal(new ArtistPictureSettingsValidationError(
            ArtistPictureSettingsField.FanartTvApiKey,
            SettingsErrorReason.Invalid));
    }
}
