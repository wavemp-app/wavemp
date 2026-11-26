using AwesomeAssertions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Settings.Tests.Validations;

[TestFixture]
public class LibrarySettingsValidatorTests
{
    [Test]
    public void Validate_Defaults_HasNoErrors()
    {
        LibrarySettings defaults = new();

        IReadOnlyList<SettingsValidationError> errors =
            LibrarySettingsValidator.Validate(LibrarySettingsValidator.Normalize(defaults));

        errors.Should().BeEmpty();
        LibrarySettingsValidator.Normalize(defaults).Should().BeEquivalentTo(defaults);
    }

    [Test]
    public void Normalize_Lists_TrimsLowersRemovesDotsEmptyAndDuplicateEntries()
    {
        LibrarySettings settings = new()
        {
            AudioExtensions = [" .MP3", "flac", "", "  ", "Mp3", "..ogg"],
            AlbumCoverNames = [" Cover ", "FOLDER", "cover"],
            ArtistPictureNames = [" Photo", "ARTIST", "", "photo"],
        };

        LibrarySettings normalized = LibrarySettingsValidator.Normalize(settings);

        normalized.AudioExtensions.Should().Equal("mp3", "flac", "ogg");
        normalized.AlbumCoverNames.Should().Equal("cover", "folder");
        normalized.ArtistPictureNames.Should().Equal("photo", "artist");
    }

    [Test]
    public void ArtistPictureNames_ByDefault_AreArtistFolderAndPhoto()
    {
        new LibrarySettings().ArtistPictureNames.Should().Equal("artist", "folder", "photo");
    }

    [TestCase(".artist")]
    [TestCase("artists/photo")]
    [TestCase("artists\\photo")]
    [TestCase("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijk")]
    public void Validate_InvalidArtistPictureName_IsInvalid(string name)
    {
        LibrarySettings settings = new() { ArtistPictureNames = ["artist", name] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.ArtistPictureNames, SettingsErrorReason.Invalid));
    }

    [Test]
    public void Validate_TooManyArtistPictureNames_IsReportedAndNoNamesAreValid()
    {
        LibrarySettings tooMany = new()
        {
            ArtistPictureNames = [.. Enumerable.Range(0, 51).Select(index => $"artist{index}")],
        };

        LibrarySettingsValidator.Validate(tooMany).Should().Equal(new SettingsValidationError(
            LibrarySettingsField.ArtistPictureNames,
            SettingsErrorReason.TooMany,
            Maximum: 50));
        LibrarySettingsValidator.Validate(new LibrarySettings { ArtistPictureNames = [] }).Should().BeEmpty();
    }

    [TestCase(14, true)]
    [TestCase(15, false)]
    [TestCase(1440, false)]
    [TestCase(1441, true)]
    [TestCase(-1, true)]
    public void Validate_ScanInterval_MustBeWithinRange(int minutes, bool invalid)
    {
        LibrarySettings settings = new() { ScanIntervalMinutes = minutes };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        if (invalid)
        {
            errors.Should().Equal(
                new SettingsValidationError(
                    LibrarySettingsField.ScanIntervalMinutes,
                    SettingsErrorReason.OutOfRange,
                    15,
                    1440));
        }
        else
        {
            errors.Should().BeEmpty();
        }
    }

    [TestCase("mp-3")]
    [TestCase("abcdefghijk")]
    [TestCase("mp3!")]
    [TestCase("ä")]
    public void Validate_ExtensionWithInvalidForm_IsInvalid(string extension)
    {
        LibrarySettings settings = LibrarySettingsValidator.Normalize(new LibrarySettings
        {
            ImageExtensions = ["jpg", extension],
        });

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.ImageExtensions, SettingsErrorReason.Invalid));
    }

    [Test]
    public void Validate_ExtensionInTwoLists_ReportsTheLaterList()
    {
        LibrarySettings settings = new() { AudioExtensions = ["mp3", "mp4"], VideoExtensions = ["mp4", "mkv"] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.VideoExtensions, SettingsErrorReason.Overlap));
    }

    [Test]
    public void Validate_ExtensionOfLyricsFiles_IsAnOverlap()
    {
        LibrarySettings settings = new() { AudioExtensions = ["mp3", "lrc"] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.AudioExtensions, SettingsErrorReason.Overlap));
    }

    [Test]
    public void DownloadLyrics_ByDefault_IsOff()
    {
        new LibrarySettings().DownloadLyrics.Should().BeFalse();
        LibrarySettingsValidator.Normalize(new LibrarySettings { DownloadLyrics = true }).DownloadLyrics
            .Should().BeTrue();
    }

    [Test]
    public void SubsonicDownloads_ByDefault_AreOff()
    {
        new LibrarySettings().SubsonicDownloads.Should().BeFalse();
        LibrarySettingsValidator.Normalize(new LibrarySettings { SubsonicDownloads = true }).SubsonicDownloads
            .Should().BeTrue();
    }

    [Test]
    public void Validate_ImageExtensionThatIsAlsoVideo_IsAnOverlap()
    {
        LibrarySettings settings = new() { ImageExtensions = ["jpg", "mkv"] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.ImageExtensions, SettingsErrorReason.Overlap));
    }

    [Test]
    public void Validate_TooManyEntries_IsReported()
    {
        LibrarySettings settings = new()
        {
            AudioExtensions = [.. Enumerable.Range(0, 51).Select(index => $"a{index}")],
            AlbumCoverNames = [.. Enumerable.Range(0, 51).Select(index => $"cover{index}")],
        };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().BeEquivalentTo(
        [
            new SettingsValidationError(LibrarySettingsField.AudioExtensions, SettingsErrorReason.TooMany, Maximum: 50),
            new SettingsValidationError(LibrarySettingsField.AlbumCoverNames, SettingsErrorReason.TooMany, Maximum: 50),
        ]);
    }

    [Test]
    public void Validate_NoAudioAndNoVideoExtensions_IsEmpty()
    {
        LibrarySettings settings = new() { AudioExtensions = [], VideoExtensions = [], ImageExtensions = [] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().BeEquivalentTo(
        [
            new SettingsValidationError(LibrarySettingsField.AudioExtensions, SettingsErrorReason.Empty),
            new SettingsValidationError(LibrarySettingsField.VideoExtensions, SettingsErrorReason.Empty),
        ]);
    }

    [Test]
    public void Validate_OnlyVideoExtensionsAndNoCoverNames_IsValid()
    {
        LibrarySettings settings = new() { AudioExtensions = [], ImageExtensions = [], AlbumCoverNames = [] };

        LibrarySettingsValidator.Validate(settings).Should().BeEmpty();
    }

    [TestCase(".cover")]
    [TestCase("covers/front")]
    [TestCase("covers\\front")]
    [TestCase("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijk")]
    public void Validate_InvalidCoverName_IsInvalid(string name)
    {
        LibrarySettings settings = new() { AlbumCoverNames = ["cover", name] };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.AlbumCoverNames, SettingsErrorReason.Invalid));
    }

    [Test]
    public void CoverRules_ByDefault_AreOwnCoverFirstAndArtistPicture()
    {
        new LibrarySettings().TrackCoverPriority.Should().Be(CoverPriority.TrackFirst);
        new LibrarySettings().AlbumCoverFallback.Should().Be(AlbumCoverFallback.ArtistPicture);
    }

    [Test]
    public void Normalize_CoverRules_KeepsTheValues()
    {
        LibrarySettings settings = new()
        {
            TrackCoverPriority = CoverPriority.TrackOnly,
            AlbumCoverFallback = AlbumCoverFallback.None,
        };

        LibrarySettings normalized = LibrarySettingsValidator.Normalize(settings);

        normalized.TrackCoverPriority.Should().Be(CoverPriority.TrackOnly);
        normalized.AlbumCoverFallback.Should().Be(AlbumCoverFallback.None);
    }

    [Test]
    public void Validate_UndefinedTrackCoverPriority_IsInvalid()
    {
        LibrarySettings settings = new() { TrackCoverPriority = (CoverPriority)7 };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.TrackCoverPriority, SettingsErrorReason.Invalid));
    }

    [Test]
    public void Validate_UndefinedAlbumCoverFallback_IsInvalid()
    {
        LibrarySettings settings = new() { AlbumCoverFallback = (AlbumCoverFallback)7 };

        IReadOnlyList<SettingsValidationError> errors = LibrarySettingsValidator.Validate(settings);

        errors.Should().Equal(
            new SettingsValidationError(LibrarySettingsField.AlbumCoverFallback, SettingsErrorReason.Invalid));
    }
}
