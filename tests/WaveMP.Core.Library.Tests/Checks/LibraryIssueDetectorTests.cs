using AwesomeAssertions;
using WaveMP.Core.Library.Checks;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Checks;

[TestFixture]
public class LibraryIssueDetectorTests
{
    private static readonly TimeSpan _tolerance = TimeSpan.FromSeconds(5);
    private static readonly Guid _artist = Guid.NewGuid();
    private static readonly Guid _otherArtist = Guid.NewGuid();
    private static readonly Guid _album = Guid.NewGuid();

    [Test]
    public void Detect_CompleteTrackWithAudioAndVideo_HasNoProblems()
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true);

        LibraryIssueDetector.Detect([track], [], _tolerance).Should().BeEmpty();
    }

    [Test]
    public void Detect_TrackWithoutCover_IsAMissingCover()
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true, hasCover: false);

        LibraryIssueDetector.Detect([track], [], _tolerance)
            .Should().Equal(new DetectedLibraryIssue(LibraryIssueType.MissingCover, track.TrackId));
    }

    [Test]
    public void Detect_TagsWithoutArtistAndAlbum_AreMissingTagsWithTheirNames()
    {
        LibraryCheckTrackRow track = Track(
            "Song",
            hasVideo: true,
            missingTags: [MetadataTag.Artist, MetadataTag.Album]);

        DetectedLibraryIssue issue = LibraryIssueDetector.Detect([track], [], _tolerance)
            .Should().ContainSingle().Which;

        issue.Type.Should().Be(LibraryIssueType.MissingTags);
        issue.TrackId.Should().Be(track.TrackId);
        issue.MissingTags.Should().Equal(MetadataTag.Artist, MetadataTag.Album);
    }

    [Test]
    public void Detect_TrackWithoutAlbum_IsAnUnknownAlbum()
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true, withAlbum: false);

        LibraryIssueDetector.Detect([track], [], _tolerance)
            .Should().Equal(new DetectedLibraryIssue(LibraryIssueType.UnknownAlbum, track.TrackId));
    }

    [Test]
    public void Detect_VideoWithoutAudio_IsAHint()
    {
        LibraryCheckTrackRow track = Track("Clip", hasAudio: false, hasVideo: true);

        LibraryIssueDetector.Detect([track], [], _tolerance)
            .Should().Equal(new DetectedLibraryIssue(LibraryIssueType.VideoWithoutAudio, track.TrackId));
    }

    [Test]
    public void Detect_AudioWithoutVideo_IsAHint()
    {
        LibraryCheckTrackRow track = Track("Song");

        LibraryIssueDetector.Detect([track], [], _tolerance)
            .Should().Equal(new DetectedLibraryIssue(LibraryIssueType.AudioWithoutVideo, track.TrackId));
    }

    [Test]
    public void Detect_UnreadableFiles_AreProblemsWithTheirTrackIfAny()
    {
        var trackId = Guid.NewGuid();
        var broken = Guid.NewGuid();
        var picture = Guid.NewGuid();

        IReadOnlyList<DetectedLibraryIssue> issues = LibraryIssueDetector.Detect(
            [],
            [
                new LibraryCheckFileRow(broken, trackId, MediaFileIssue.ProbeFailed),
                new LibraryCheckFileRow(picture, null, MediaFileIssue.Unreadable),
            ],
            _tolerance);

        issues.Should().Equal(
            new DetectedLibraryIssue(LibraryIssueType.UnreadableFile, trackId, broken, MediaFileIssue.ProbeFailed),
            new DetectedLibraryIssue(LibraryIssueType.UnreadableFile, null, picture, MediaFileIssue.Unreadable));
    }

    [Test]
    public void Detect_SameArtistAndTitleWithSimilarDuration_ArePossibleDuplicatesOfTheFirstOtherTrack()
    {
        LibraryCheckTrackRow first = Track("Song", seconds: 200, hasVideo: true);
        LibraryCheckTrackRow second = Track("  song ", seconds: 203, hasVideo: true);
        LibraryCheckTrackRow third = Track("SONG", seconds: 198, hasVideo: true);

        IReadOnlyList<DetectedLibraryIssue> issues = LibraryIssueDetector.Detect(
            [first, second, third],
            [],
            _tolerance);

        issues.Should().Equal(
            new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, first.TrackId, RelatedTrackId: second.TrackId),
            new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, second.TrackId, RelatedTrackId: first.TrackId),
            new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, third.TrackId, RelatedTrackId: first.TrackId));
    }

    [Test]
    public void FindDuplicates_DurationsFurtherApartThanTheTolerance_AreDifferentRecordings()
    {
        LibraryCheckTrackRow studio = Track("Song", seconds: 200);
        LibraryCheckTrackRow live = Track("Song", seconds: 260);
        LibraryCheckTrackRow liveCopy = Track("Song", seconds: 262);
        LibraryCheckTrackRow other = Track("Song", seconds: 200, artistId: _otherArtist);

        Dictionary<Guid, Guid> duplicates =
            LibraryIssueDetector.FindDuplicates([studio, live, liveCopy, other], _tolerance);

        duplicates.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [live.TrackId] = liveCopy.TrackId,
            [liveCopy.TrackId] = live.TrackId,
        });
    }

    [Test]
    public void FindDuplicates_DurationsInAChain_FormOneGroup()
    {
        LibraryCheckTrackRow first = Track("Song", seconds: 200);
        LibraryCheckTrackRow second = Track("Song", seconds: 204);
        LibraryCheckTrackRow third = Track("Song", seconds: 208);

        LibraryIssueDetector.FindDuplicates([first, second, third], _tolerance).Keys
            .Should().BeEquivalentTo([first.TrackId, second.TrackId, third.TrackId]);
    }

    [Test]
    public void FindDuplicates_UnknownArtistOrDuration_IsNotCompared()
    {
        LibraryCheckTrackRow withoutArtist = Track("Song", seconds: 200, withArtist: false);
        LibraryCheckTrackRow alsoWithoutArtist = Track("Song", seconds: 200, withArtist: false);
        LibraryCheckTrackRow withoutDuration = Track("Song", seconds: null);
        LibraryCheckTrackRow alsoWithoutDuration = Track("Song", seconds: null);
        LibraryCheckTrackRow single = Track("Song", seconds: 100);

        LibraryIssueDetector.FindDuplicates(
                [withoutArtist, alsoWithoutArtist, withoutDuration, alsoWithoutDuration, single],
                _tolerance)
            .Should().BeEmpty();
    }

    [Test]
    public void LibraryIssueTypes_Hints_AreTheVideosWithoutAudioTheSongsWithoutVideoAndPossibleSeveralArtists()
    {
        LibraryIssueTypes.OfSeverity(LibraryIssueSeverity.Hint).Should().Equal(
            LibraryIssueType.VideoWithoutAudio,
            LibraryIssueType.AudioWithoutVideo,
            LibraryIssueType.PossibleMultipleArtists);
        LibraryIssueTypes.All.Should().OnlyHaveUniqueItems().And.HaveCount(Enum.GetValues<LibraryIssueType>().Length);
    }

    [TestCase("Simon & Garfunkel")]
    [TestCase("Earth, Wind & Fire")]
    [TestCase("AC/DC")]
    public void Detect_AmbiguousArtistNameWithoutValues_IsAHintForSeveralArtists(string name)
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true) with { ArtistNames = [name] };

        LibraryIssueDetector.Detect([track], [], _tolerance).Should().Equal(
            new DetectedLibraryIssue(LibraryIssueType.PossibleMultipleArtists, track.TrackId));
    }

    [Test]
    public void Detect_AmbiguousArtistNameWithConfirmedValues_IsNoHint()
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true) with
        {
            ArtistNames = ["Simon & Garfunkel"],
            HasArtistValues = true,
        };

        LibraryIssueDetector.Detect([track], [], _tolerance).Should().BeEmpty();
    }

    [Test]
    public void Detect_UnambiguousArtistNames_AreNoHint()
    {
        LibraryCheckTrackRow track = Track("Song", hasVideo: true) with { ArtistNames = ["K.Flay", "Lil Nas X"] };

        LibraryIssueDetector.Detect([track], [], _tolerance).Should().BeEmpty();
    }

    private static LibraryCheckTrackRow Track(
        string title,
        int? seconds = 200,
        bool hasAudio = true,
        bool hasVideo = false,
        bool hasCover = true,
        IReadOnlyList<MetadataTag>? missingTags = null,
        Guid? artistId = null,
        bool withArtist = true,
        bool withAlbum = true)
    {
        return new LibraryCheckTrackRow(
            Guid.NewGuid(),
            title,
            withArtist ? artistId ?? _artist : null,
            withAlbum ? _album : null,
            seconds is { } value ? TimeSpan.FromSeconds(value) : null,
            hasAudio,
            hasVideo,
            hasCover,
            missingTags ?? []);
    }
}
