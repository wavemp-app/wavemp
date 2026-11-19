using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Library.Options;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.OnlineMetadata.Tests.Covers;

/// <summary>
/// Stores covers in a media folder and a data folder of the test.
/// </summary>
[TestFixture]
public class OnlineCoverWriterTests
{
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _albumId = Guid.NewGuid();
    private static readonly Guid _releaseId = Guid.NewGuid();

    private TemporaryDirectory _media = null!;
    private TemporaryDirectory _data = null!;
    private IOnlineMetadataRepository _repository = null!;
    private FolderCoverFiles _files = null!;
    private IMediaFileLocator _locator = null!;
    private ILibraryScanner _scanner = null!;
    private FakeSnapshot<LibrarySettings> _librarySettings = null!;
    private OnlineCoverWriter _writer = null!;

    [SetUp]
    public void SetUp()
    {
        _media = new TemporaryDirectory();
        _data = new TemporaryDirectory();
        _repository = Substitute.For<IOnlineMetadataRepository>();
        _repository.SetOnlineCoverAsync(Arg.Any<OnlineCover>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));
        _repository.RemoveOnlineCoverAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files = new FolderCoverFiles(_data.Combine("online-covers"));
        _locator = Substitute.For<IMediaFileLocator>();
        _locator.TryGetFullPath(_sourceId, Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
        {
            call[2] = _media.Combine(call.ArgAt<string>(1));
            return true;
        });
        _scanner = Substitute.For<ILibraryScanner>();
        _librarySettings = new FakeSnapshot<LibrarySettings>(new LibrarySettings());
        _writer = new OnlineCoverWriter(
            _repository,
            _files,
            _locator,
            _scanner,
            _librarySettings,
            Microsoft.Extensions.Options.Options.Create(
                new OnlineCoverOptions { RootPath = _data.Combine("online-covers") }),
            new SimulatedClock(),
            NullLogger<OnlineCoverWriter>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _media.Dispose();
        _data.Dispose();
    }

    [Test]
    public async Task SaveAsync_WritableFolder_WritesThePictureAfterTheCoverRulesAndReadsTheFolder()
    {
        _media.CreateDirectory("Band/Album");

        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.MediaFolder);
        File.ReadAllBytes(_media.Combine("Band/Album/cover.jpg")).Should().Equal(_jpeg);
        Directory.EnumerateFiles(_media.Combine("Band/Album")).Should().ContainSingle();
        await _scanner.Received(1).RefreshFolderAsync(
            _sourceId,
            "Band/Album",
            Arg.Is<IReadOnlyCollection<string>>(changed => changed.SequenceEqual(new[] { "Band/Album/cover.jpg" })),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).RemoveOnlineCoverAsync(_albumId, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAsync_PictureOfSameName_ReplacesItAndKeepsACopy()
    {
        _media.CreateDirectory("Band/Album");
        File.WriteAllBytes(_media.Combine("Band/Album/Cover.png"), [1, 2, 3]);
        File.WriteAllBytes(_media.Combine("Band/Album/Song.mp3"), [4]);

        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.MediaFolder);
        Directory.EnumerateFiles(_media.Combine("Band/Album")).Select(Path.GetFileName)
            .Should().BeEquivalentTo("cover.jpg", "Song.mp3");
        string copy = Directory.EnumerateFiles(
                _data.Combine("online-covers/" + OnlineCoverWriter.ReplacedFolderName),
                "*",
                SearchOption.AllDirectories)
            .Should().ContainSingle().Subject;
        Path.GetFileName(copy).Should().Be("Cover.png");
        File.ReadAllBytes(copy).Should().Equal(1, 2, 3);
        await _scanner.Received(1).RefreshFolderAsync(
            _sourceId,
            "Band/Album",
            Arg.Is<IReadOnlyCollection<string>>(changed =>
                changed.Order().SequenceEqual(new[] { "Band/Album/Cover.png", "Band/Album/cover.jpg" }.Order())),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAsync_SongCover_IsNamedAfterTheSong()
    {
        var trackId = Guid.NewGuid();
        _media.CreateDirectory("Singles");

        CoverSaveOutcome outcome = await _writer.SaveAsync(
            new CoverTarget(null, trackId, _sourceId, "Singles", "Song"),
            _jpeg,
            _releaseId);

        outcome.Should().Be(CoverSaveOutcome.MediaFolder);
        File.Exists(_media.Combine("Singles/Song.jpg")).Should().BeTrue();
    }

    [Test]
    public async Task SaveAsync_FolderNotReachable_KeepsTheCoverInTheDataFolder()
    {
        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.DataFolder);
        string stored = _files.ListFileNames().Should().ContainSingle().Subject;
        await _repository.Received(1).SetOnlineCoverAsync(
            Arg.Is<OnlineCover>(cover =>
                cover.AlbumId == _albumId
                && cover.TrackId == null
                && cover.FileName == stored
                && cover.MimeType == "image/jpeg"
                && cover.Size == _jpeg.Length
                && cover.ReleaseId == _releaseId),
            Arg.Any<CancellationToken>());
        _scanner.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_ImageTypeNotReadByTheLibrary_KeepsTheCoverInTheDataFolder()
    {
        _media.CreateDirectory("Band/Album");
        _librarySettings.Current = new LibrarySettings { ImageExtensions = ["png"] };

        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.DataFolder);
        Directory.EnumerateFiles(_media.Combine("Band/Album")).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_EarlierOnlineCover_IsReplaced()
    {
        _repository.SetOnlineCoverAsync(Arg.Any<OnlineCover>(), Arg.Any<CancellationToken>())
            .Returns((true, "old.jpg"));
        await _files.SaveAsync("old.jpg", _jpeg);

        await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        _files.ListFileNames().Should().ContainSingle().Which.Should().NotBe("old.jpg");
    }

    [Test]
    public async Task SaveAsync_OwnerGone_RemovesThePictureAgain()
    {
        _repository.SetOnlineCoverAsync(Arg.Any<OnlineCover>(), Arg.Any<CancellationToken>())
            .Returns((false, (string?)null));

        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.NotSaved);
        _files.ListFileNames().Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_DataFolderNotWritable_IsNotSaved()
    {
        _files.FailSaving = true;

        CoverSaveOutcome outcome = await _writer.SaveAsync(AlbumTarget(), _jpeg, _releaseId);

        outcome.Should().Be(CoverSaveOutcome.NotSaved);
        _repository.ReceivedCalls().Should().BeEmpty();
    }

    [TestCase("image/jpeg", "jpg")]
    [TestCase("image/png", "png")]
    [TestCase("image/avif", null)]
    [TestCase(null, null)]
    public void ExtensionOf_KnownType_IsAnImageExtensionOfTheLibrary(string? contentType, string? expected)
    {
        OnlineCoverWriter.ExtensionOf(contentType, new LibrarySettings().ImageExtensions).Should().Be(expected);
    }

    [Test]
    public void ExtensionOf_OnlyJpegSpelling_UsesIt()
    {
        OnlineCoverWriter.ExtensionOf("image/jpeg", ["jpeg"]).Should().Be("jpeg");
    }

    private static CoverTarget AlbumTarget()
    {
        return new CoverTarget(_albumId, null, _sourceId, "Band/Album", "cover");
    }
}
