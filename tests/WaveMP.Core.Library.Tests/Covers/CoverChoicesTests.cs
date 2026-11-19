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

    [Test]
    public void ForAlbum_AlbumFirstWithAlbumCover_ListsTheAlbumAndEveryTrackWithOwnCovers()
    {
        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.AlbumFirst, [_second, _album, _first]);

        // The album, its tracks without own cover and the first track share one list; the second track puts its own
        // cover before the cover of the first track.
        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([_album, _first, _second]),
            CoverChoices.KeyOf([_album, _second, _first]),
        ]);
        lists.Should().AllSatisfy(list => list[0].Should().Be(_album));
    }

    [Test]
    public void ForAlbum_TrackFirst_ListsTheAlbumEveryTrackWithOwnCoversAndTheOthers()
    {
        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.TrackFirst, [_second, _album, _first]);

        CoverCandidateRow[] ofFirst = [_first, _album, _second];
        CoverCandidateRow[] ofSecond = [_second, _album, _first];
        bool firstIsLower = _firstTrackId.CompareTo(_secondTrackId) < 0;

        lists.Select(CoverChoices.KeyOf).Should().Equal(
            CoverChoices.KeyOf([_first, _second, _album]),
            CoverChoices.KeyOf(firstIsLower ? ofFirst : ofSecond),
            CoverChoices.KeyOf(firstIsLower ? ofSecond : ofFirst),
            CoverChoices.KeyOf([_album, _first, _second]));
    }

    [Test]
    public void ForAlbum_RuleOfTheAlbum_OverridesTheGlobalRule()
    {
        CoverCandidateRow album = _album with { AlbumCoverPriority = CoverPriority.TrackFirst };
        CoverCandidateRow first = _first with { AlbumCoverPriority = CoverPriority.TrackFirst };
        CoverCandidateRow second = _second with { AlbumCoverPriority = CoverPriority.TrackFirst };

        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.AlbumFirst, [second, album, first]);

        lists[0].Should().Equal(first, second, album);
        lists.Where(list => list[0] == album).Should().ContainSingle()
            .Which.Should().Equal(album, first, second);
    }

    [Test]
    public void ForAlbum_RuleOfATrack_OverridesTheRuleOfTheAlbumOnlyForThatTrack()
    {
        CoverCandidateRow second = _second with { TrackCoverPriority = CoverPriority.TrackFirst };

        IReadOnlyList<IReadOnlyList<CoverCandidateRow>> lists =
            CoverChoices.ForAlbum(CoverPriority.AlbumFirst, [second, _album, _first]);

        lists.Select(CoverChoices.KeyOf).Should().BeEquivalentTo(
        [
            CoverChoices.KeyOf([_album, _first, second]),
            CoverChoices.KeyOf([second, _album, _first]),
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

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.TrackFirst, [second, album, first])
            .Should().Equal(album, first, second);
        CoverChoices.ForTrack(_secondTrackId, CoverPriority.AlbumFirst, [second, album, first])
            .Should().Equal(second, album, first);
    }

    [Test]
    public void ForTrack_PreferredCoverMissing_FallsBackToTheOther()
    {
        CoverCandidateRow first = _first with { TrackCoverPriority = CoverPriority.AlbumFirst };

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.TrackFirst, [first]).Should().Equal(first);
        CoverChoices.ForTrack(_secondTrackId, CoverPriority.TrackFirst, [_album]).Should().Equal(_album);
    }

    [TestCase(null, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    public void PreviewForAlbum_Rule_OrdersAsIfTheAlbumHadIt(CoverPriority? rule, CoverPriority expected)
    {
        // The stored rule of the album is replaced by the previewed one; inherit falls back to the global rule.
        CoverCandidateRow album = _album with { AlbumCoverPriority = CoverPriority.TrackFirst };
        CoverCandidateRow first = _first with { AlbumCoverPriority = CoverPriority.TrackFirst };

        IReadOnlyList<CoverCandidateRow> ordered =
            CoverChoices.PreviewForAlbum(CoverPriority.AlbumFirst, rule, [first, album]);

        ordered.Should().Equal(expected == CoverPriority.AlbumFirst ? [album, first] : [first, album]);
    }

    [TestCase(null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    public void PreviewForTrack_Rule_OrdersAsIfTheTrackHadIt(CoverPriority? rule, CoverPriority expected)
    {
        // The album says TrackFirst, the stored rule of the track AlbumFirst; inherit previews the rule of the album.
        CoverCandidateRow album = _album with { AlbumCoverPriority = CoverPriority.TrackFirst };
        CoverCandidateRow first = _first with
        {
            AlbumCoverPriority = CoverPriority.TrackFirst,
            TrackCoverPriority = CoverPriority.AlbumFirst,
        };

        IReadOnlyList<CoverCandidateRow> ordered =
            CoverChoices.PreviewForTrack(_firstTrackId, CoverPriority.AlbumFirst, rule, [album, first]);

        ordered.Should().Equal(expected == CoverPriority.AlbumFirst ? [album, first] : [first, album]);
    }

    [Test]
    public void PreviewForTrack_SelectedCover_StaysFirst()
    {
        CoverCandidateRow selected = _first with { IsSelected = true };

        CoverChoices
            .PreviewForTrack(_firstTrackId, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, [_album, selected])
            .Should().Equal(selected, _album);
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

        CoverChoices.ForTrack(_firstTrackId, CoverPriority.AlbumFirst, [embedded, _first])
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
