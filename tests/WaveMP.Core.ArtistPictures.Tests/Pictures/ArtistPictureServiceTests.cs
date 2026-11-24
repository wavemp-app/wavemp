using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.ArtistPictures.Tests.Pictures;

[TestFixture]
public class ArtistPictureServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 24, 9, 30, 15, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset _stored = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _artistId = Guid.NewGuid();
    private readonly ArtistLocalPictureRow _local = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Band/artist.jpg",
        4096,
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ArtistStoredPictureRow _uploaded = new(Guid.NewGuid(), _stored);
    private readonly ArtistStoredPictureRow _online = new(Guid.NewGuid(), _stored.AddDays(1));

    private IArtistPictureRepository _repository = null!;
    private IArtistPictureStore _store = null!;
    private ICoverImageCache _covers = null!;
    private ArtistPictureService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IArtistPictureRepository>();
        _store = Substitute.For<IArtistPictureStore>();
        _covers = Substitute.For<ICoverImageCache>();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now.AddTicks(7));
        _service = new ArtistPictureService(
            _repository,
            _store,
            _covers,
            timeProvider,
            NullLogger<ArtistPictureService>.Instance);
    }

    [Test]
    public async Task GetShownAsync_UnknownArtist_ReturnsOwnerNotFound()
    {
        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 300);

        result.Should().Be(CoverDeliveryResult.OwnerNotFound);
    }

    [Test]
    public async Task GetShownAsync_PictureInTheArtistFolder_DeliversItThroughTheCoverCache()
    {
        GivenSources(_local, _uploaded, _online);
        CoverDelivery scaled = new(Stream.Null, "image/webp", "scaled", _local.ModifiedAt);
        _covers.DeliverAsync(Arg.Any<IReadOnlyList<CoverCandidateRow>>(), 300, Arg.Any<CancellationToken>())
            .Returns(scaled);

        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 300);

        result.Should().Be(CoverDeliveryResult.Found(scaled));
        await _covers.Received(1).DeliverAsync(
            Arg.Is<IReadOnlyList<CoverCandidateRow>>(candidates => candidates.Count == 1
                && candidates[0].MediaFileId == _local.MediaFileId
                && candidates[0].Source == CoverSource.File
                && candidates[0].SourceId == _local.SourceId
                && candidates[0].RelativePath == _local.RelativePath
                && candidates[0].Size == _local.Size
                && candidates[0].ModifiedAt == _local.ModifiedAt),
            300,
            Arg.Any<CancellationToken>());
        _store.DidNotReceiveWithAnyArgs().Open(default, default);
    }

    [Test]
    public async Task GetShownAsync_WithoutSize_AsksForTheLargeSize()
    {
        GivenSources(_local, null, null);

        await _service.GetShownAsync(_artistId, null);

        await _covers.Received(1).DeliverAsync(
            Arg.Any<IReadOnlyList<CoverCandidateRow>>(),
            CoverSizes.Large,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetShownAsync_UnreadablePictureInTheArtistFolder_DeliversTheUploadedPicture()
    {
        GivenSources(_local, _uploaded, _online);
        _store.Open(_uploaded.Id, 300).Returns(new MemoryStream([1, 2, 3]));

        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 200);

        result.Status.Should().Be(CoverStatus.Found);
        result.Delivery!.ETag.Should().Be($"u-{_uploaded.Id:N}-300");
        result.Delivery.ContentType.Should().Be("image/webp");
        result.Delivery.LastModified.Should().Be(_uploaded.CreatedAt);
    }

    [Test]
    public async Task GetShownAsync_OnlyOnline_DeliversTheOnlinePicture()
    {
        GivenSources(null, null, _online);
        _store.Open(_online.Id, 1000).Returns(new MemoryStream([1]));

        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 1500);

        result.Delivery!.ETag.Should().Be($"o-{_online.Id:N}-1000");
        await _covers.DidNotReceiveWithAnyArgs().DeliverAsync(default!, default);
    }

    [Test]
    public async Task GetShownAsync_UploadedFilesMissing_DeliversTheOnlinePicture()
    {
        GivenSources(null, _uploaded, _online);
        _store.Open(_online.Id, 96).Returns(new MemoryStream([1]));

        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 96);

        result.Delivery!.ETag.Should().Be($"o-{_online.Id:N}-96");
    }

    [Test]
    public async Task GetShownAsync_NoPicture_ReturnsNotFound()
    {
        GivenSources(null, null, null);

        CoverDeliveryResult result = await _service.GetShownAsync(_artistId, 96);

        result.Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task GetStoredAsync_UploadedHiddenByTheArtistFolder_DeliversTheUploadedPicture()
    {
        GivenSources(_local, _uploaded, _online);
        _store.Open(_uploaded.Id, 1000).Returns(new MemoryStream([1]));

        CoverDeliveryResult result = await _service.GetStoredAsync(_artistId, ArtistPictureKind.Uploaded, null);

        result.Delivery!.ETag.Should().Be($"u-{_uploaded.Id:N}-1000");
        await _covers.DidNotReceiveWithAnyArgs().DeliverAsync(default!, default);
    }

    [Test]
    public async Task GetStoredAsync_NoneOfTheKind_ReturnsNotFound()
    {
        GivenSources(_local, _uploaded, null);

        (await _service.GetStoredAsync(_artistId, ArtistPictureKind.Online, 96))
            .Should().Be(CoverDeliveryResult.NotFound);
        (await _service.GetStoredAsync(Guid.NewGuid(), ArtistPictureKind.Online, 96))
            .Should().Be(CoverDeliveryResult.OwnerNotFound);
    }

    [Test]
    public async Task UploadAsync_Saved_StoresThePictureAndRemovesTheReplacedFiles()
    {
        GivenSources(null, _uploaded, null);
        Guid storedId = Guid.Empty;
        _store.SaveAsync(Arg.Do<Guid>(id => storedId = id), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 640, 640));
        ArtistPicture? replaced = null;
        _repository.ReplaceAsync(Arg.Do<ArtistPicture>(picture => replaced = picture), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureReplacement(true, [_uploaded.Id]));

        ArtistPictureUploadStatus status = await _service.UploadAsync(_artistId, Stream.Null);

        status.Should().Be(ArtistPictureUploadStatus.Saved);
        replaced.Should().BeEquivalentTo(new
        {
            Id = storedId,
            ArtistId = _artistId,
            Kind = ArtistPictureKind.Uploaded,
            CreatedAt = new DateTimeOffset(_now.UtcDateTime, TimeSpan.Zero),
            Width = 640,
            Height = 640,
            Provider = (ArtistPictureProvider?)null,
        });
        storedId.Should().NotBe(_uploaded.Id);
        _store.Received(1).Delete(_uploaded.Id);
        _store.DidNotReceive().Delete(storedId);
    }

    [TestCase(ArtistPictureSaveStatus.UnsupportedFormat, ArtistPictureUploadStatus.UnsupportedFormat)]
    [TestCase(ArtistPictureSaveStatus.Unreadable, ArtistPictureUploadStatus.Unreadable)]
    [TestCase(ArtistPictureSaveStatus.Unavailable, ArtistPictureUploadStatus.Unavailable)]
    public async Task UploadAsync_NotSaved_ReturnsWhyAndKeepsTheEarlierPicture(
        ArtistPictureSaveStatus saved,
        ArtistPictureUploadStatus expected)
    {
        GivenSources(null, _uploaded, null);
        _store.SaveAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ArtistPictureSaveResult.Failed(saved));

        ArtistPictureUploadStatus status = await _service.UploadAsync(_artistId, Stream.Null);

        status.Should().Be(expected);
        await _repository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        _store.DidNotReceiveWithAnyArgs().Delete(default);
    }

    [Test]
    public async Task UploadAsync_UnknownArtist_StoresNothing()
    {
        ArtistPictureUploadStatus status = await _service.UploadAsync(_artistId, Stream.Null);

        status.Should().Be(ArtistPictureUploadStatus.ArtistNotFound);
        await _store.DidNotReceiveWithAnyArgs().SaveAsync(default, default!);
    }

    [Test]
    public async Task UploadAsync_ArtistRemovedDuringTheUpload_RemovesTheNewFiles()
    {
        GivenSources(null, null, null);
        Guid storedId = Guid.Empty;
        _store.SaveAsync(Arg.Do<Guid>(id => storedId = id), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 96, 96));
        _repository.ReplaceAsync(Arg.Any<ArtistPicture>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureReplacement(false, []));

        ArtistPictureUploadStatus status = await _service.UploadAsync(_artistId, Stream.Null);

        status.Should().Be(ArtistPictureUploadStatus.ArtistNotFound);
        _store.Received(1).Delete(storedId);
    }

    [Test]
    public async Task RemoveAsync_StoredPicture_RemovesItsFiles()
    {
        _repository.RemoveAsync(_artistId, ArtistPictureKind.Online, Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureRemoval(_online.Id));

        (await _service.RemoveAsync(_artistId, ArtistPictureKind.Online)).Should().BeTrue();

        _store.Received(1).Delete(_online.Id);
    }

    [Test]
    public async Task RemoveAsync_NoPictureOrNoArtist_RemovesNoFiles()
    {
        _repository.RemoveAsync(_artistId, ArtistPictureKind.Uploaded, Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureRemoval(null));

        (await _service.RemoveAsync(_artistId, ArtistPictureKind.Uploaded)).Should().BeTrue();
        (await _service.RemoveAsync(Guid.NewGuid(), ArtistPictureKind.Uploaded)).Should().BeFalse();

        _store.DidNotReceiveWithAnyArgs().Delete(default);
    }

    [Test]
    public async Task RemoveOrphansAsync_FoldersWithoutPicture_RemovesOnlyThose()
    {
        var kept = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        _store.ListPictureIds().Returns([kept, orphan]);
        _repository.ListExistingIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
                Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { kept });

        await _service.RemoveOrphansAsync();

        _store.Received(1).Delete(orphan);
        _store.DidNotReceive().Delete(kept);
    }

    [Test]
    public async Task RemoveOrphansAsync_EmptyStore_DoesNotAskTheDatabase()
    {
        _store.ListPictureIds().Returns([]);

        await _service.RemoveOrphansAsync();

        await _repository.DidNotReceiveWithAnyArgs().ListExistingIdsAsync(default!);
    }

    private void GivenSources(
        ArtistLocalPictureRow? local,
        ArtistStoredPictureRow? uploaded,
        ArtistStoredPictureRow? online)
    {
        _repository.FindSourcesAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureSources(_artistId, local, uploaded, online));
    }
}
