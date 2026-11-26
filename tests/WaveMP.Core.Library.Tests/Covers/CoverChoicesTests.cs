using AwesomeAssertions;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class CoverChoicesTests
{
    private static readonly Guid _albumId = Guid.NewGuid();
    private static readonly Guid _firstTrackId = Guid.NewGuid();
    private static readonly Guid _secondTrackId = Guid.NewGuid();

    private static readonly CoverCandidateRow _album = Cover(albumId: _albumId);
    private static readonly CoverCandidateRow _first = Cover(trackId: _firstTrackId, number: 1);
    private static readonly CoverCandidateRow _second = Cover(trackId: _secondTrackId, number: 2);

    private static bool FirstIsLower => _firstTrackId.CompareTo(_secondTrackId) < 0;

    [Test]
    public void ForAlbum_TrackFirst_ListsTheAlbumAndEveryTrackWithItsOwnCoverFirst()
    {
        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.TrackFirst, [_second, _album, _first]);

        // The album never shows the picture with the name of a song; every song shows its own picture and then the
        // cover of the album, never the picture of another song; the songs without a picture of their own show the
        // cover of the album, the same list as the album.
        lists.Select(CoverChoices.KeyOf).Should().Equal(
            CoverChoices.KeyOf([_album]),
            CoverChoices.KeyOf(FirstIsLower ? [_first, _album] : [_second, _album]),
            CoverChoices.KeyOf(FirstIsLower ? [_second, _album] : [_first, _album]));
    }

    [Test]
    public void ForAlbum_AlbumFirst_PutsTheAlbumCoverFirstInEveryList()
    {
        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.AlbumFirst, [_second, _album, _first]);

        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([_album]),
            CoverChoices.KeyOf([_album, _first]),
            CoverChoices.KeyOf([_album, _second]),
        ]);
        lists.Should().AllSatisfy(list => list[0].Should().Be(_album));
    }

    [Test]
    public void ForAlbum_TrackOnly_NeverGivesASongTheAlbumCover()
    {
        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.TrackOnly, [_second, _album, _first]);

        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([_album]),
            CoverChoices.KeyOf([_first]),
            CoverChoices.KeyOf([_second]),
        ]);
    }

    [Test]
    public void ForAlbum_RuleOfTheAlbum_OverridesTheGlobalRuleForItsSongs()
    {
        CoverCandidateRow album = _album with { AlbumCoverPriority = CoverPriority.AlbumFirst };
        CoverCandidateRow first = _first with { AlbumCoverPriority = CoverPriority.AlbumFirst };

        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.TrackFirst, [album, first]);

        lists.Select(CoverChoices.KeyOf).Should().Equal(
            CoverChoices.KeyOf([album]),
            CoverChoices.KeyOf([album, first]));
    }

    [Test]
    public void ForAlbum_RuleOfATrack_OverridesTheRuleOfTheAlbumOnlyForThatTrack()
    {
        CoverCandidateRow second = _second with { TrackCoverPriority = CoverPriority.TrackOnly };

        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.AlbumFirst, [second, _album, _first]);

        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([_album]),
            CoverChoices.KeyOf([_album, _first]),
            CoverChoices.KeyOf([second]),
        ]);
    }

    [Test]
    public void ForAlbum_EmbeddedPicture_IsTheCoverOfTheAlbumAndOfItsSongs()
    {
        CoverCandidateRow embedded = _first with { Source = CoverSource.Embedded };

        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.TrackFirst, [embedded, _second]);

        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([embedded]),
            CoverChoices.KeyOf([_second, embedded]),
        ]);
    }

    [Test]
    public void ForTrack_RuleOfTheTrackAndOfTheAlbum_TheTrackWins()
    {
        CoverCandidateRow album = _album with { AlbumCoverPriority = CoverPriority.TrackFirst };
        CoverCandidateRow first = _first with
        {
            AlbumCoverPriority = CoverPriority.TrackFirst,
            TrackCoverPriority = CoverPriority.AlbumFirst,
        };
        CoverCandidateRow second = _second with { AlbumCoverPriority = CoverPriority.TrackFirst };

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.TrackOnly, [second, album, first])
            .Should().Equal(album, first);
        CoverChoices.ForTrack(_secondTrackId, CoverPriority.AlbumFirst, [second, album, first])
            .Should().Equal(second, album);
    }

    [Test]
    public void ForTrack_PreferredCoverMissing_FallsBackToTheOther()
    {
        CoverCandidateRow first = _first with { TrackCoverPriority = CoverPriority.AlbumFirst };

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.TrackFirst, [first]).Should().Equal(first);
        CoverChoices.ForTrack(_secondTrackId, CoverPriority.TrackFirst, [_album]).Should().Equal(_album);
    }

    [Test]
    public void ForAlbum_WithoutCovers_ListsNothing()
    {
        CoverChoices.ForAlbum(CoverPriority.AlbumFirst, []).Should().BeEmpty();
    }

    [Test]
    public void ForTrack_LooseTrack_ListsItsCovers()
    {
        CoverCandidateRow embedded = _first with
        {
            CoverId = Guid.NewGuid(),
            Source = CoverSource.Embedded,
            IsPrimary = false,
        };

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.TrackFirst, [embedded, _first])
            .Should().Equal(_first, embedded);
    }

    [Test]
    public void KeyOf_SamePicturesOfOtherCovers_IsTheSame()
    {
        CoverCandidateRow sameFile = _album with { CoverId = Guid.NewGuid(), AlbumId = Guid.NewGuid() };
        CoverCandidateRow embedded = _album with { Source = CoverSource.Embedded };

        CoverChoices.KeyOf([_album, _first]).Should().Be(CoverChoices.KeyOf([sameFile, _first]));
        CoverChoices.KeyOf([_album]).Should().NotBe(CoverChoices.KeyOf([embedded]));
        CoverChoices.KeyOf([_album, _first]).Should().NotBe(CoverChoices.KeyOf([_first, _album]));
    }

    private static CoverCandidateRow Cover(Guid? albumId = null, Guid? trackId = null, int? number = null)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            albumId,
            trackId,
            trackId is null ? null : _albumId,
            trackId is null ? null : 1,
            number,
            trackId is null ? null : $"Track {number}",
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            "Album/picture.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch);
    }
}
