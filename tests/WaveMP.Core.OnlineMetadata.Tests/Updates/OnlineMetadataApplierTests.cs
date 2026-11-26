using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.OnlineMetadata.Updates;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Settings.Groups;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Tests.Updates;

/// <summary>
/// Applying matches with a simulated metadata editor, Cover Art Archive and cover writer.
/// </summary>
[TestFixture]
public class OnlineMetadataApplierTests
{
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly Guid _trackId = Guid.NewGuid();
    private static readonly Guid _albumId = Guid.NewGuid();
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _releaseId = Guid.NewGuid();

    private FakeSnapshot<OnlineMetadataSettings> _settings = null!;
    private FakeSnapshot<LibrarySettings> _librarySettings = null!;
    private IOnlineMetadataRepository _repository = null!;
    private ITrackMetadataEditor _editor = null!;
    private ICoverArtArchiveClient _coverArtArchive = null!;
    private IOnlineCoverWriter _covers = null!;
    private OnlineMetadataApplier _applier = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = new FakeSnapshot<OnlineMetadataSettings>(new OnlineMetadataSettings { Enabled = true });
        _librarySettings = new FakeSnapshot<LibrarySettings>(new LibrarySettings());
        _repository = Substitute.For<IOnlineMetadataRepository>();
        _editor = Substitute.For<ITrackMetadataEditor>();
        _coverArtArchive = Substitute.For<ICoverArtArchiveClient>();
        _covers = Substitute.For<IOnlineCoverWriter>();
        _applier = new OnlineMetadataApplier(
            _settings,
            _librarySettings,
            _repository,
            _editor,
            _coverArtArchive,
            _covers,
            NullLogger<OnlineMetadataApplier>.Instance);

        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(new OnlineTrackRow(
            _trackId,
            _sourceId,
            "Band/Album",
            "01 Song",
            _albumId,
            TimeSpan.FromSeconds(200),
            null,
            null));
        _repository.FindAlbumAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new OnlineAlbumRow(
            _albumId,
            [new OnlineAlbumTrackRow(_trackId, _sourceId, "Band/Album", null)]));
        _repository.FindAlbumIdOfTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(_albumId);
        _editor.UpdateAsync(Arg.Any<Guid>(), Arg.Any<TagChanges>(), Arg.Any<CancellationToken>())
            .Returns(call => TrackMetadataResult.Success(Metadata(call.ArgAt<Guid>(0))));
        _editor.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Metadata(call.ArgAt<Guid>(0)));
        _coverArtArchive.GetFrontCoverAsync(_releaseId, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Found(_jpeg));
        _covers.SaveAsync(Arg.Any<CoverTarget>(), Arg.Any<byte[]>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CoverSaveOutcome.MediaFolder);
    }

    [Test]
    public async Task ApplyToTrackAsync_Disabled_ChangesNothing()
    {
        _settings.Current = new OnlineMetadataSettings { Enabled = false };

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, TitleChange(), _releaseId);

        result.Error.Should().Be(OnlineApplyError.Disabled);
        _editor.ReceivedCalls().Should().BeEmpty();
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task ApplyToTrackAsync_AlbumFirstRule_WritesThroughTheEditorAndStoresTheCoverOfTheAlbum()
    {
        _librarySettings.Current = new LibrarySettings { TrackCoverPriority = CoverPriority.AlbumFirst };
        TagChanges changes = TitleChange();

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, changes, _releaseId);

        result.Error.Should().BeNull();
        result.Cover.Should().Be(CoverSaveOutcome.MediaFolder);
        await _editor.Received(1).UpdateAsync(_trackId, changes, Arg.Any<CancellationToken>());
        await _coverArtArchive.Received(1)
            .GetFrontCoverAsync(_releaseId, CoverArtSize.ExtraLarge, Arg.Any<CancellationToken>());
        await _covers.Received(1).SaveAsync(
            CoverTarget.ForAlbum(_albumId),
            _jpeg,
            _releaseId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyToTrackAsync_DefaultRule_StoresTheCoverOfTheSong()
    {
        await _applier.ApplyToTrackAsync(_trackId, new TagChanges(), _releaseId);

        await _covers.Received(1).SaveAsync(
            new CoverTarget(null, _trackId, _sourceId, "Band/Album", "01 Song"),
            _jpeg,
            _releaseId,
            Arg.Any<CancellationToken>());
        await _editor.DidNotReceive()
            .UpdateAsync(Arg.Any<Guid>(), Arg.Any<TagChanges>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyToTrackAsync_CoverArchiveUnavailable_WritesNoTags()
    {
        _coverArtArchive.GetFrontCoverAsync(_releaseId, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable));

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, TitleChange(), _releaseId);

        result.Error.Should().Be(OnlineApplyError.Unavailable);
        await _editor.DidNotReceive()
            .UpdateAsync(Arg.Any<Guid>(), Arg.Any<TagChanges>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyToTrackAsync_NoLargeCover_UsesTheSmallerOne()
    {
        _coverArtArchive.GetFrontCoverAsync(_releaseId, CoverArtSize.ExtraLarge, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Failed(OnlineStatus.NotFound));

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, new TagChanges(), _releaseId);

        result.Error.Should().BeNull();
        await _coverArtArchive.Received(1)
            .GetFrontCoverAsync(_releaseId, CoverArtSize.Large, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyToTrackAsync_NoCoverAtAll_IsCoverNotFound()
    {
        _coverArtArchive.GetFrontCoverAsync(_releaseId, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Failed(OnlineStatus.NotFound));

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, TitleChange(), _releaseId);

        result.Error.Should().Be(OnlineApplyError.CoverNotFound);
        _editor.ReceivedCalls().Should().BeEmpty();
    }

    [TestCase(TrackMetadataError.NoAudio, OnlineApplyError.NoAudio)]
    [TestCase(TrackMetadataError.NotWritable, OnlineApplyError.NotWritable)]
    public async Task ApplyToTrackAsync_EditorRefuses_ReturnsTheReasonAndStoresNoCover(
        TrackMetadataError error,
        OnlineApplyError expected)
    {
        _editor.UpdateAsync(_trackId, Arg.Any<TagChanges>(), Arg.Any<CancellationToken>())
            .Returns(TrackMetadataResult.Failure(error));

        TrackApplyResult result = await _applier.ApplyToTrackAsync(_trackId, TitleChange(), _releaseId);

        result.Error.Should().Be(expected);
        _covers.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task ApplyToTrackAsync_UnknownTrack_IsTrackNotFound()
    {
        TrackApplyResult result = await _applier.ApplyToTrackAsync(Guid.NewGuid(), TitleChange(), null);

        result.Error.Should().Be(OnlineApplyError.TrackNotFound);
    }

    [Test]
    public async Task ApplyToAlbumAsync_Changes_WritesOnlyAlbumFieldsIntoEveryTrackAndCountsSkipped()
    {
        var second = Guid.NewGuid();
        var renamed = Guid.NewGuid();
        _repository.FindAlbumAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new OnlineAlbumRow(
            _albumId,
            [
                new OnlineAlbumTrackRow(_trackId, _sourceId, "Band/Album", null),
                new OnlineAlbumTrackRow(second, _sourceId, "Band/Album", null),
            ]));
        _repository.FindAlbumIdOfTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(renamed);
        _repository.FindAlbumAsync(renamed, Arg.Any<CancellationToken>()).Returns(new OnlineAlbumRow(
            renamed,
            [new OnlineAlbumTrackRow(_trackId, _sourceId, "Band/Album", null)]));
        _editor.UpdateAsync(second, Arg.Any<TagChanges>(), Arg.Any<CancellationToken>())
            .Returns(TrackMetadataResult.Failure(TrackMetadataError.NotWritable));
        TagChanges changes = new()
        {
            Title = new TagChange<string?>("Ignored"),
            Album = new TagChange<string?>("New Album"),
            Year = new TagChange<int?>(1999),
        };

        AlbumApplyResult result = await _applier.ApplyToAlbumAsync(_albumId, changes, _releaseId);

        result.Should().Be(new AlbumApplyResult(null, renamed, 1, 1, CoverSaveOutcome.MediaFolder));
        await _editor.Received(1).UpdateAsync(
            _trackId,
            Arg.Is<TagChanges>(written =>
                written.Title == null
                && written.Album == new TagChange<string?>("New Album")
                && written.Year == new TagChange<int?>(1999)),
            Arg.Any<CancellationToken>());
        await _covers.Received(1).SaveAsync(
            CoverTarget.ForAlbum(renamed),
            _jpeg,
            _releaseId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyToAlbumAsync_UnknownAlbum_IsAlbumNotFound()
    {
        AlbumApplyResult result = await _applier.ApplyToAlbumAsync(Guid.NewGuid(), new TagChanges(), _releaseId);

        result.Error.Should().Be(OnlineApplyError.AlbumNotFound);
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task ApplyToAlbumAsync_Disabled_ChangesNothing()
    {
        _settings.Current = new OnlineMetadataSettings { Enabled = false };

        AlbumApplyResult result = await _applier.ApplyToAlbumAsync(_albumId, new TagChanges(), _releaseId);

        result.Error.Should().Be(OnlineApplyError.Disabled);
        _editor.ReceivedCalls().Should().BeEmpty();
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    private static TagChanges TitleChange()
    {
        return new TagChanges { Title = new TagChange<string?>("New Song") };
    }

    private static TrackMetadata Metadata(Guid id)
    {
        return new TrackMetadata(
            id,
            TrackMetadataStatus.Editable,
            "Band/Album/01 Song.mp3",
            "Song",
            "Band",
            "Album",
            "Band",
            null,
            1,
            1,
            null);
    }
}
