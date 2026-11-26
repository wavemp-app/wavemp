using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class CoverServiceTests
{
    private readonly Guid _trackId = Guid.NewGuid();
    private readonly Guid _albumId = Guid.NewGuid();

    private ICoverRepository _covers = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private CoverService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _covers = Substitute.For<ICoverRepository>();
        _covers.ListAlbumIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns([]);
        _covers.ListLooseTrackIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns([]);
        _covers.GetTrackRuleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(null, null));
        _covers.GetAlbumCoverFallbackAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AlbumCoverFallbackRow(null, null));
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings());
        _service = new CoverService(_covers, _settings);
    }

    [Test]
    public async Task GetTrackCandidatesAsync_UnknownTrack_ReturnsOwnerNotFound()
    {
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CoverCandidateRow>?)null);

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.Should().Be(CoverCandidates.OwnerNotFound);
        candidates.OwnerExists.Should().BeFalse();
    }

    [Test]
    public async Task GetTrackCandidatesAsync_AlbumAndOwnCover_PutsTheOwnCoverFirstByDefault()
    {
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow own = TrackCover(_trackId);
        CoverCandidateRow other = TrackCover(Guid.NewGuid());
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([album, other, own]);

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.OwnerExists.Should().BeTrue();
        candidates.Ordered.Should().Equal(own, album);
        candidates.FallbackArtistId.Should().BeNull();
    }

    [Test]
    public async Task GetTrackCandidatesAsync_GlobalAlbumFirst_PutsTheAlbumCoverFirst()
    {
        _settings.Current.Returns(new LibrarySettings { TrackCoverPriority = CoverPriority.AlbumFirst });
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow own = TrackCover(_trackId);
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([own, album]);

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.Ordered.Should().Equal(album, own);
    }

    [Test]
    public async Task GetTrackCandidatesAsync_GlobalTrackOnly_NeverShowsTheAlbumCover()
    {
        _settings.Current.Returns(new LibrarySettings { TrackCoverPriority = CoverPriority.TrackOnly });
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow embedded = TrackCover(_trackId) with { Source = CoverSource.Embedded };
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([album, embedded]);

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.OwnerExists.Should().BeTrue();
        candidates.Ordered.Should().BeEmpty();
    }

    [Test]
    public async Task GetTrackCandidatesAsync_RuleOfTheAlbum_OverridesTheGlobalRule()
    {
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow own = TrackCover(_trackId);
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([album, own]);
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new CoverRuleRow(null, CoverPriority.AlbumFirst));

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.Ordered.Should().Equal(album, own);
    }

    [Test]
    public async Task GetTrackCandidatesAsync_RuleOfTheTrackWithoutOwnCovers_StillApplies()
    {
        // The rule of the track is read on its own: a track without covers of its own carries none in its rows.
        CoverCandidateRow album = AlbumCover();
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([album]);
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new CoverRuleRow(CoverPriority.TrackOnly, CoverPriority.AlbumFirst));

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.Ordered.Should().BeEmpty();
    }

    [Test]
    public async Task GetTrackCandidatesAsync_UnknownRules_ReturnsOwnerNotFound()
    {
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([AlbumCover()]);
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>()).Returns((CoverRuleRow?)null);

        CoverCandidates candidates = await _service.GetTrackCandidatesAsync(_trackId);

        candidates.OwnerExists.Should().BeFalse();
    }

    [Test]
    public async Task PreviewTrackCandidatesAsync_OtherRule_OrdersByItWithoutTheStoredRule()
    {
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow own = TrackCover(_trackId);
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>()).Returns([album, own]);
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new CoverRuleRow(CoverPriority.TrackOnly, CoverPriority.AlbumFirst));

        CoverCandidates trackFirst = await _service.PreviewTrackCandidatesAsync(_trackId, CoverPriority.TrackFirst);
        CoverCandidates inherited = await _service.PreviewTrackCandidatesAsync(_trackId, null);

        trackFirst.Ordered.Should().Equal(own, album);
        inherited.Ordered.Should().Equal(album, own);
    }

    [Test]
    public async Task PreviewTrackCandidatesAsync_UnknownTrack_ReturnsOwnerNotFound()
    {
        _covers.GetTrackCandidatesAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CoverCandidateRow>?)null);

        CoverCandidates candidates = await _service.PreviewTrackCandidatesAsync(_trackId, CoverPriority.TrackFirst);

        candidates.OwnerExists.Should().BeFalse();
    }

    [Test]
    public async Task GetAlbumCandidatesAsync_UnknownAndKnownAlbum_ReturnsOwnerNotFoundOrItsOwnCovers()
    {
        CoverCandidateRow own = TrackCover(_trackId);
        CoverCandidateRow embedded = TrackCover(_trackId) with { Source = CoverSource.Embedded };
        CoverCandidateRow album = AlbumCover();
        _covers.GetAlbumCandidatesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CoverCandidateRow>?)null);
        _covers.GetAlbumCandidatesAsync(_albumId, Arg.Any<CancellationToken>()).Returns([own, embedded, album]);

        CoverCandidates missing = await _service.GetAlbumCandidatesAsync(Guid.NewGuid());
        CoverCandidates known = await _service.GetAlbumCandidatesAsync(_albumId);

        missing.OwnerExists.Should().BeFalse();
        known.Ordered.Should().Equal(album, embedded);
    }

    [TestCase(AlbumCoverFallback.ArtistPicture, null, true)]
    [TestCase(AlbumCoverFallback.None, null, false)]
    [TestCase(AlbumCoverFallback.None, AlbumCoverFallback.ArtistPicture, true)]
    [TestCase(AlbumCoverFallback.ArtistPicture, AlbumCoverFallback.None, false)]
    public async Task GetAlbumCandidatesAsync_Fallback_NamesTheAlbumArtistOnlyIfItsPictureMayBeShown(
        AlbumCoverFallback global,
        AlbumCoverFallback? album,
        bool showsArtist)
    {
        var artistId = Guid.NewGuid();
        _settings.Current.Returns(new LibrarySettings { AlbumCoverFallback = global });
        _covers.GetAlbumCandidatesAsync(_albumId, Arg.Any<CancellationToken>()).Returns([]);
        _covers.GetAlbumCoverFallbackAsync(_albumId, Arg.Any<CancellationToken>())
            .Returns(new AlbumCoverFallbackRow(album, artistId));

        CoverCandidates candidates = await _service.GetAlbumCandidatesAsync(_albumId);

        candidates.Ordered.Should().BeEmpty();
        candidates.FallbackArtistId.Should().Be(showsArtist ? artistId : null);
    }

    [Test]
    public async Task GetAlbumCandidatesAsync_RuleOfTheSongs_DoesNotChangeTheCoverOfTheAlbum()
    {
        CoverCandidateRow album = AlbumCover() with { AlbumCoverPriority = CoverPriority.TrackOnly };
        CoverCandidateRow own = TrackCover(_trackId) with { AlbumCoverPriority = CoverPriority.TrackOnly };
        _covers.GetAlbumCandidatesAsync(_albumId, Arg.Any<CancellationToken>()).Returns([own, album]);

        CoverCandidates candidates = await _service.GetAlbumCandidatesAsync(_albumId);
        CoverCandidates preview = await _service.PreviewAlbumCandidatesAsync(_albumId, CoverPriority.TrackFirst);

        candidates.Ordered.Should().Equal(album);
        preview.Ordered.Should().Equal(album);
    }

    [Test]
    public async Task EnumerateShownCoversAsync_RulesOfAlbumAndTrack_ListsTheCoversShownByThem()
    {
        CoverCandidateRow album = AlbumCover() with { AlbumCoverPriority = CoverPriority.AlbumFirst };
        CoverCandidateRow own = TrackCover(_trackId) with { AlbumCoverPriority = CoverPriority.AlbumFirst };
        _covers.ListAlbumIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns([_albumId]);
        _covers.GetAlbumBatchCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([album, own]);

        List<IReadOnlyList<CoverCandidateRow>> lists = [];
        await foreach (IReadOnlyList<CoverCandidateRow> list in _service.EnumerateShownCoversAsync())
        {
            lists.Add(list);
        }

        // The album shows its cover; by the rule of the album its songs show the album cover first as well.
        lists.Select(CoverChoices.KeyOf).Should().Equal(
            CoverChoices.KeyOf([album]),
            CoverChoices.KeyOf([album, own]));
    }

    [Test]
    public async Task EnumerateShownCoversAsync_AlbumsAndLooseTracks_ListsEveryDistinctListOnce()
    {
        var otherAlbumId = Guid.NewGuid();
        var looseId = Guid.NewGuid();
        CoverCandidateRow album = AlbumCover();
        CoverCandidateRow own = TrackCover(_trackId);
        CoverCandidateRow otherAlbum = AlbumCover() with { AlbumId = otherAlbumId };
        CoverCandidateRow loose = TrackCover(looseId) with { TrackAlbumId = null };
        _covers.ListAlbumIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns([_albumId, otherAlbumId]);
        _covers.GetAlbumBatchCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([own, otherAlbum, album]);
        _covers.ListLooseTrackIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns([looseId]);
        _covers.GetTrackBatchCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([loose]);

        List<IReadOnlyList<CoverCandidateRow>> lists = [];
        await foreach (IReadOnlyList<CoverCandidateRow> list in _service.EnumerateShownCoversAsync())
        {
            lists.Add(list);
        }

        lists.Select(CoverChoices.KeyOf).Should().OnlyHaveUniqueItems();
        lists.Should().ContainEquivalentOf(new[] { own, album }, options => options.WithStrictOrdering());
        lists.Should().ContainEquivalentOf(new[] { album }, options => options.WithStrictOrdering());
        lists.Should().ContainEquivalentOf(new[] { otherAlbum }, options => options.WithStrictOrdering());
        lists.Should().ContainEquivalentOf(new[] { loose }, options => options.WithStrictOrdering());
        lists.Should().HaveCount(4);
    }

    [Test]
    public async Task EnumerateShownCoversAsync_ManyAlbums_ReadsThemInBatches()
    {
        Guid[] albumIds = [.. Enumerable.Range(0, CoverService.AlbumBatchSize + 1).Select(_ => Guid.NewGuid())];
        _covers.ListAlbumIdsWithCoversAsync(Arg.Any<CancellationToken>()).Returns(albumIds);
        _covers.GetAlbumBatchCandidatesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await foreach (IReadOnlyList<CoverCandidateRow> _ in _service.EnumerateShownCoversAsync())
        {
        }

        await _covers.Received(1).GetAlbumBatchCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == CoverService.AlbumBatchSize),
            Arg.Any<CancellationToken>());
        await _covers.Received(1).GetAlbumBatchCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1),
            Arg.Any<CancellationToken>());
    }

    private CoverCandidateRow AlbumCover()
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
            "Album/cover.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch);
    }

    private CoverCandidateRow TrackCover(Guid trackId)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            trackId,
            _albumId,
            1,
            1,
            "Song",
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            "Album/Song.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch);
    }
}
