using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Caching.Tests.Covers;

[TestFixture]
public class CoverCacheServiceTests
{
    private readonly Guid _id = Guid.NewGuid();
    private ICoverService _covers = null!;
    private ICoverImageCache _cache = null!;
    private IAlbumCoverFallback _fallback = null!;
    private CoverCacheService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _covers = Substitute.For<ICoverService>();
        _cache = Substitute.For<ICoverImageCache>();
        _fallback = Substitute.For<IAlbumCoverFallback>();
        _service = new CoverCacheService(_covers, _cache, [_fallback]);
    }

    [Test]
    public async Task GetTrackCoverAsync_UnknownTrack_ReturnsOwnerNotFound()
    {
        _covers.GetTrackCandidatesAsync(_id, Arg.Any<CancellationToken>()).Returns(CoverCandidates.OwnerNotFound);

        CoverDeliveryResult result = await _service.GetTrackCoverAsync(_id, 300);

        result.Should().Be(CoverDeliveryResult.OwnerNotFound);
        await _cache.DidNotReceiveWithAnyArgs().DeliverAsync(default!, default, default);
    }

    [Test]
    public async Task GetTrackCoverAsync_WithCovers_PassesTheOrderedCoversAndTheSize()
    {
        CoverCandidateRow candidate = Candidate();
        CoverDelivery delivery = new(new MemoryStream([1]), "image/webp", "etag", DateTimeOffset.UnixEpoch);
        _covers.GetTrackCandidatesAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [candidate]));
        _cache.DeliverAsync(
                Arg.Is<IReadOnlyList<CoverCandidateRow>>(list => list.Single() == candidate),
                96,
                Arg.Any<CancellationToken>())
            .Returns(delivery);

        CoverDeliveryResult result = await _service.GetTrackCoverAsync(_id, 96);

        result.Should().Be(CoverDeliveryResult.Found(delivery));
    }

    [Test]
    public async Task GetAlbumCoverAsync_NothingDeliverable_ReturnsNotFound()
    {
        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [Candidate()]));
        _cache.DeliverAsync(Arg.Any<IReadOnlyList<CoverCandidateRow>>(), null, Arg.Any<CancellationToken>())
            .Returns((CoverDelivery?)null);

        CoverDeliveryResult result = await _service.GetAlbumCoverAsync(_id, null);

        result.Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task GetAlbumCoverAsync_NoOwnCoverAndArtistAllowed_DeliversThePictureOfTheArtist()
    {
        var artistId = Guid.NewGuid();
        CoverDelivery picture = new(new MemoryStream([2]), "image/webp", "artist", DateTimeOffset.UnixEpoch);
        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [], artistId));
        _cache.DeliverAsync(Arg.Any<IReadOnlyList<CoverCandidateRow>>(), 300, Arg.Any<CancellationToken>())
            .Returns((CoverDelivery?)null);
        _fallback.DeliverArtistPictureAsync(artistId, 300, Arg.Any<CancellationToken>()).Returns(picture);

        CoverDeliveryResult result = await _service.GetAlbumCoverAsync(_id, 300);

        result.Should().Be(CoverDeliveryResult.Found(picture));
    }

    [Test]
    public async Task GetAlbumCoverAsync_OwnCover_NeverAsksForThePictureOfTheArtist()
    {
        CoverDelivery delivery = new(new MemoryStream([1]), "image/webp", "etag", DateTimeOffset.UnixEpoch);
        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [Candidate()], Guid.NewGuid()));
        _cache.DeliverAsync(Arg.Any<IReadOnlyList<CoverCandidateRow>>(), 300, Arg.Any<CancellationToken>())
            .Returns(delivery);

        CoverDeliveryResult result = await _service.GetAlbumCoverAsync(_id, 300);

        result.Should().Be(CoverDeliveryResult.Found(delivery));
        await _fallback.DidNotReceiveWithAnyArgs().DeliverArtistPictureAsync(default, default, default);
    }

    [Test]
    public async Task GetAlbumCoverAsync_ArtistNotAllowedOrWithoutPicture_ReturnsNotFound()
    {
        var artistId = Guid.NewGuid();
        _cache.DeliverAsync(Arg.Any<IReadOnlyList<CoverCandidateRow>>(), null, Arg.Any<CancellationToken>())
            .Returns((CoverDelivery?)null);
        _fallback.DeliverArtistPictureAsync(artistId, null, Arg.Any<CancellationToken>())
            .Returns((CoverDelivery?)null);

        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>()).Returns(new CoverCandidates(true, []));
        CoverDeliveryResult notAllowed = await _service.GetAlbumCoverAsync(_id, null);
        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [], artistId));
        CoverDeliveryResult withoutPicture = await _service.GetAlbumCoverAsync(_id, null);

        notAllowed.Should().Be(CoverDeliveryResult.NotFound);
        withoutPicture.Should().Be(CoverDeliveryResult.NotFound);
        await _fallback.Received(1).DeliverArtistPictureAsync(artistId, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAlbumCoverAsync_UnknownAlbum_ReturnsOwnerNotFound()
    {
        _covers.GetAlbumCandidatesAsync(_id, Arg.Any<CancellationToken>()).Returns(CoverCandidates.OwnerNotFound);

        (await _service.GetAlbumCoverAsync(_id, 300)).Status.Should().Be(CoverStatus.OwnerNotFound);
    }

    [Test]
    public async Task PreviewTrackCoverAsync_Rule_DeliversTheCoversOrderedByIt()
    {
        CoverCandidateRow candidate = Candidate();
        CoverDelivery delivery = new(new MemoryStream([1]), "image/webp", "etag", DateTimeOffset.UnixEpoch);
        _covers.PreviewTrackCandidatesAsync(_id, CoverPriority.TrackFirst, Arg.Any<CancellationToken>())
            .Returns(new CoverCandidates(true, [candidate]));
        _cache.DeliverAsync(
                Arg.Is<IReadOnlyList<CoverCandidateRow>>(list => list.Single() == candidate),
                300,
                Arg.Any<CancellationToken>())
            .Returns(delivery);

        CoverDeliveryResult result = await _service.PreviewTrackCoverAsync(_id, CoverPriority.TrackFirst, 300);

        result.Should().Be(CoverDeliveryResult.Found(delivery));
    }

    [Test]
    public async Task PreviewAlbumCoverAsync_UnknownAlbum_ReturnsOwnerNotFound()
    {
        _covers.PreviewAlbumCandidatesAsync(_id, null, Arg.Any<CancellationToken>())
            .Returns(CoverCandidates.OwnerNotFound);

        CoverDeliveryResult result = await _service.PreviewAlbumCoverAsync(_id, null, 96);

        result.Should().Be(CoverDeliveryResult.OwnerNotFound);
    }

    private CoverCandidateRow Candidate()
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _id,
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
}
