using AwesomeAssertions;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class CoverResolverTests
{
    private static readonly Guid _albumId = Guid.NewGuid();
    private static readonly Guid _trackId = Guid.NewGuid();
    private static readonly Guid _firstTrackId = Guid.NewGuid();
    private static readonly Guid _secondTrackId = Guid.NewGuid();

    private static readonly CoverCandidateRow _album = AlbumCover("album");
    private static readonly CoverCandidateRow _own = TrackCover("own", _trackId, 1, 5) with
    {
        Source = CoverSource.Embedded,
    };
    private static readonly CoverCandidateRow _ownFile = TrackCover("own-file", _trackId, 1, 5);
    private static readonly CoverCandidateRow _secondTrack = TrackCover("second", _secondTrackId, 2, 1);
    private static readonly CoverCandidateRow _firstTrack = TrackCover("first", _firstTrackId, 1, 1);

    [Test]
    public void OrderForTrack_AlbumFirst_PutsTheAlbumCoverBeforeTheEmbeddedCoverAndLeavesOtherTracksOut()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_secondTrack, _own, _firstTrack, _album]);

        ordered.Should().Equal(_album, _own);
    }

    [Test]
    public void OrderForTrack_TrackFirst_PutsTheOwnCoverFirst()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [_secondTrack, _album, _firstTrack, _own]);

        ordered.Should().Equal(_own, _album);
    }

    [TestCase(CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst)]
    public void OrderForTrack_PictureFileWithTheNameOfTheTrack_ComesBeforeTheAlbumCoverWhateverTheRule(
        CoverPriority priority)
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            priority,
            [_own, _album, _ownFile]);

        ordered[0].Should().Be(_ownFile);
    }

    [Test]
    public void OrderForTrack_PictureFileWithAlbumFirstRuleOfTheTrack_FollowsTheRule()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_ownFile, _album],
            ownFileFirst: false);

        ordered.Should().Equal(_album, _ownFile);
    }

    [Test]
    public void OrderForTrack_SelectedOwnCover_ComesFirstWhateverTheRule()
    {
        CoverCandidateRow selected = _own with { IsSelected = true };
        CoverCandidateRow otherSelected = _firstTrack with { IsSelected = true };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_secondTrack, otherSelected, _ownFile, _album, selected]);

        ordered.Should().Equal(selected, _ownFile, _album);
    }

    [Test]
    public void OrderForTrack_WithoutAlbumCover_FallsBackToTheOwnCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_firstTrack, _own]);

        ordered.Should().Equal(_own);
    }

    [Test]
    public void OrderForTrack_OnlyCoversOfOtherTracks_ShowsNoCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [_firstTrack, _secondTrack]);

        ordered.Should().BeEmpty();
    }

    [Test]
    public void OrderForTrack_SeveralOwnCovers_PutsThePrimaryAndPictureFilesFirst()
    {
        CoverCandidateRow embedded = _ownFile with
        {
            CoverId = Guid.NewGuid(),
            Source = CoverSource.Embedded,
            IsPrimary = false,
        };
        CoverCandidateRow secondary = _ownFile with { CoverId = Guid.NewGuid(), IsPrimary = false };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [embedded, secondary, _ownFile]);

        ordered.Should().Equal(_ownFile, secondary, embedded);
    }

    [Test]
    public void OrderForTrack_OnlineCover_ComesFirstInItsGroup()
    {
        CoverCandidateRow online = _own with
        {
            CoverId = Guid.NewGuid(),
            Source = CoverSource.Online,
            SourceId = Guid.Empty,
        };
        CoverCandidateRow onlineAlbum = _album with
        {
            CoverId = Guid.NewGuid(),
            Source = CoverSource.Online,
            SourceId = Guid.Empty,
        };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_own, _album, online, onlineAlbum]);

        ordered.Should().Equal(onlineAlbum, _album, online, _own);
    }

    [TestCase(CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst)]
    public void OrderForAlbum_Always_FollowsTheRule(CoverPriority priority)
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForAlbum(
            priority,
            [_secondTrack, _album, _firstTrack]);

        if (priority == CoverPriority.AlbumFirst)
        {
            ordered.Should().Equal(_album, _firstTrack, _secondTrack);
        }
        else
        {
            ordered.Should().Equal(_firstTrack, _secondTrack, _album);
        }
    }

    private static CoverCandidateRow AlbumCover(string name)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _albumId,
            null,
            null,
            null,
            null,
            null,
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            $"Album/{name}.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch);
    }

    private static CoverCandidateRow TrackCover(string name, Guid trackId, int disc, int number)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            trackId,
            _albumId,
            disc,
            number,
            name,
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            $"Album/{name}.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch);
    }
}
