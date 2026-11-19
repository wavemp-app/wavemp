using AwesomeAssertions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Validations;

[TestFixture]
public class OnlineMetadataSettingsValidatorTests
{
    [Test]
    public void Validate_Defaults_HasNoErrors()
    {
        OnlineMetadataSettingsValidator.Validate(new OnlineMetadataSettings()).Should().BeEmpty();
    }

    [TestCase("https://musicbrainz.org", true)]
    [TestCase("http://192.168.1.20:5000", true)]
    [TestCase("https://mirror.local/musicbrainz", true)]
    [TestCase("ftp://musicbrainz.org", false)]
    [TestCase("musicbrainz.org", false)]
    [TestCase("https://user:secret@musicbrainz.org", false)]
    [TestCase("https://musicbrainz.org?x=1", false)]
    [TestCase("https://musicbrainz.org#top", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsValidUrl_AbsoluteHttpAddressWithoutExtras(string? url, bool expected)
    {
        OnlineMetadataSettingsValidator.IsValidUrl(url).Should().Be(expected);
    }

    [Test]
    public void IsValidUrl_TooLong_IsInvalid()
    {
        string url = "https://mirror.local/" + new string('a', OnlineMetadataSettingsValidator.MaxUrlLength);

        OnlineMetadataSettingsValidator.IsValidUrl(url).Should().BeFalse();
    }

    [Test]
    public void Validate_InvalidValues_NamesEachField()
    {
        OnlineMetadataSettings settings = new()
        {
            MusicBrainzUrl = "nope",
            CoverArtArchiveUrl = "file:///covers",
            MusicBrainzRequestIntervalMilliseconds = 60_001,
        };

        OnlineMetadataSettingsValidator.Validate(settings).Should().Equal(
            new OnlineMetadataSettingsValidationError(
                OnlineMetadataSettingsField.MusicBrainzUrl,
                SettingsErrorReason.Invalid),
            new OnlineMetadataSettingsValidationError(
                OnlineMetadataSettingsField.CoverArtArchiveUrl,
                SettingsErrorReason.Invalid),
            new OnlineMetadataSettingsValidationError(
                OnlineMetadataSettingsField.MusicBrainzRequestIntervalMilliseconds,
                SettingsErrorReason.OutOfRange,
                0,
                60_000));
    }

    [Test]
    public void Normalize_Addresses_TrimsSpacesAndTrailingSlashes()
    {
        OnlineMetadataSettings settings = OnlineMetadataSettingsValidator.Normalize(new OnlineMetadataSettings
        {
            Enabled = true,
            MusicBrainzUrl = " https://mirror.local/ ",
            CoverArtArchiveUrl = "https://covers.local//",
            MusicBrainzRequestIntervalMilliseconds = 10,
        });

        settings.Enabled.Should().BeTrue();
        settings.MusicBrainzUrl.Should().Be("https://mirror.local");
        settings.CoverArtArchiveUrl.Should().Be("https://covers.local");
        settings.MusicBrainzRequestIntervalMilliseconds.Should().Be(10);
    }

    [TestCase("https://musicbrainz.org", true)]
    [TestCase("https://MusicBrainz.org.", true)]
    [TestCase("https://beta.musicbrainz.org", true)]
    [TestCase("https://notmusicbrainz.org", false)]
    [TestCase("http://mirror.local:5000", false)]
    public void IsOfficialMusicBrainz_HostOfMusicBrainz(string url, bool expected)
    {
        OnlineMetadataSettings.IsOfficialMusicBrainz(url).Should().Be(expected);
    }
}
