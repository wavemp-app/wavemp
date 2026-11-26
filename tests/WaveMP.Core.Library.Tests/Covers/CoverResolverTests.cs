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
    public void OrderForTrack_TrackFirst_PutsTheOwnCoverBeforeTheAlbumCoverAndLeavesOtherTracksOut()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [_secondTrack, _album, _firstTrack, _ownFile]);

        ordered.Should().Equal(_ownFile, _album);
    }

    [Test]
    public void OrderForTrack_AlbumFirst_PutsTheAlbumCoverBeforeTheOwnCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_ownFile, _secondTrack, _album]);

        ordered.Should().Equal(_album, _ownFile);
    }

    [Test]
    public void OrderForTrack_TrackOnly_ShowsOnlyTheOwnCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackOnly,
            [_album, _own, _ownFile, _firstTrack]);

        ordered.Should().Equal(_ownFile);
    }

    [Test]
    public void OrderForTrack_TrackOnlyWithoutOwnCover_ShowsNoCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackOnly,
            [_album, _own]);

        ordered.Should().BeEmpty();
    }

    [TestCase(CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst)]
    public void OrderForTrack_WithoutOwnCover_FallsBackToTheAlbumCover(CoverPriority priority)
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            priority,
            [_firstTrack, _album]);

        ordered.Should().Equal(_album);
    }

    [TestCase(CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst)]
    public void OrderForTrack_WithoutAlbumCover_FallsBackToTheOwnCover(CoverPriority priority)
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            priority,
            [_firstTrack, _ownFile]);

        ordered.Should().Equal(_ownFile);
    }

    [Test]
    public void OrderForTrack_EmbeddedPictures_AreCoversOfTheAlbumWithTheOwnOneFirst()
    {
        CoverCandidateRow otherEmbedded = _firstTrack with { Source = CoverSource.Embedded };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [otherEmbedded, _own, _ownFile]);

        ordered.Should().Equal(_ownFile, _own, otherEmbedded);
    }

    [Test]
    public void OrderForTrack_PictureInTheAlbumFolder_ComesBeforeEmbeddedPictures()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.AlbumFirst,
            [_own, _album]);

        ordered.Should().Equal(_album, _own);
    }

    [TestCase(CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackOnly)]
    public void OrderForTrack_SelectedCover_ComesFirstWhateverTheRule(CoverPriority priority)
    {
        CoverCandidateRow selected = _own with { IsSelected = true };
        CoverCandidateRow otherSelected = _firstTrack with { IsSelected = true };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            priority,
            [_secondTrack, otherSelected, _ownFile, _album, selected]);

        ordered[0].Should().Be(selected);
        ordered.Should().NotContain(otherSelected);
    }

    [Test]
    public void OrderForTrack_OnlyPicturesOfOtherTracks_ShowsNoCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [_firstTrack, _secondTrack]);

        ordered.Should().BeEmpty();
    }

    [Test]
    public void OrderForTrack_SeveralOwnCovers_PutsThePrimaryFirst()
    {
        CoverCandidateRow secondary = _ownFile with { CoverId = Guid.NewGuid(), IsPrimary = false };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForTrack(
            _trackId,
            CoverPriority.TrackFirst,
            [secondary, _ownFile]);

        ordered.Should().Equal(_ownFile, secondary);
    }

    [Test]
    public void OrderForTrack_OnlineCover_ComesFirstInItsGroup()
    {
        CoverCandidateRow online = _ownFile with
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
            [_ownFile, _album, online, onlineAlbum]);

        ordered.Should().Equal(onlineAlbum, _album, online, _ownFile);
    }

    [Test]
    public void OrderForAlbum_ShowsOnlyCoversOfTheAlbumAndNeverPicturesWithTheNameOfASong()
    {
        CoverCandidateRow firstEmbedded = _firstTrack with { Source = CoverSource.Embedded };
        CoverCandidateRow secondEmbedded = _secondTrack with { Source = CoverSource.Embedded };
        CoverCandidateRow onlineTrack = _ownFile with { Source = CoverSource.Online, SourceId = Guid.Empty };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForAlbum(
            [secondEmbedded, _ownFile, firstEmbedded, onlineTrack, _album]);

        ordered.Should().Equal(_album, firstEmbedded, secondEmbedded);
    }

    [Test]
    public void OrderForAlbum_OnlineCover_ComesFirst()
    {
        CoverCandidateRow online = _album with
        {
            CoverId = Guid.NewGuid(),
            Source = CoverSource.Online,
            SourceId = Guid.Empty,
        };

        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForAlbum([_own, _album, online]);

        ordered.Should().Equal(online, _album, _own);
    }

    [Test]
    public void OrderForAlbum_OnlyPicturesWithTheNameOfSongs_ShowsNoCover()
    {
        IReadOnlyList<CoverCandidateRow> ordered = CoverResolver.OrderForAlbum([_ownFile, _firstTrack]);

        ordered.Should().BeEmpty();
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
