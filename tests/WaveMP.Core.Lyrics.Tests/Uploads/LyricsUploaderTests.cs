using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Parsers;
using WaveMP.Core.Lyrics.Saving;
using WaveMP.Core.Lyrics.Uploads;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Uploads;

/// <summary>
/// Uploads with substituted files and a simulated LRCLIB; nothing leaves the process.
/// </summary>
[TestFixture]
public class LyricsUploaderTests
{
    private static readonly Guid _trackId = Guid.NewGuid();
    private const string Synced = "[00:01.00]One\n[00:02.00]\n[00:03.00]Two";

    private ILyricsRepository _repository = null!;
    private ILyricsFileStore _files = null!;
    private ILrclibClient _lrclib = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private LyricsUploader _uploader = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row());
        _repository.SaveDownloadAsync(Arg.Any<LyricsDownload>(), Arg.Any<CancellationToken>()).Returns(true);
        _files = Substitute.For<ILyricsFileStore>();
        _files.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.Created);
        _files.ReplaceAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.Created);
        _lrclib = Substitute.For<ILrclibClient>();
        _lrclib.PublishAsync(Arg.Any<LrclibPublication>(), Arg.Any<CancellationToken>())
            .Returns(new LrclibPublishResult(LrclibPublishStatus.Published));
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings { DownloadLyrics = true });
        _uploader = new LyricsUploader(
            _repository,
            new LyricsSaver(
                _repository,
                _files,
                Substitute.For<ILibraryScanner>(),
                TimeProvider.System,
                NullLogger<LyricsSaver>.Instance),
            _lrclib,
            _settings,
            NullLogger<LyricsUploader>.Instance);
    }

    [Test]
    public async Task UploadAsync_Lyrics_SavesThemAsFileAndRecordsTheUpload()
    {
        LyricsUploadResult result =
            await _uploader.UploadAsync(_trackId, "\uFEFF" + Synced.Replace("\n", "\r\n"), false);

        result.Should().Be(new LyricsUploadResult(LyricsUploadOutcome.SavedToFile));
        await _files.Received(1)
            .CreateAsync(MediaSource.DefaultId, "Band/Song.lrc", Synced, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.Uploaded
                && download.Content == null
                && !download.OtherVersion),
            Arg.Any<CancellationToken>());
        await _lrclib.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [Test]
    public async Task UploadAsync_TrackWithLyricsFile_ReplacesIt()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(Row() with { LyricsFilePath = "Band/Song.LRC" });

        LyricsUploadResult result = await _uploader.UploadAsync(_trackId, "Plain line", false);

        result.Outcome.Should().Be(LyricsUploadOutcome.SavedToFile);
        await _files.Received(1).ReplaceAsync(
            MediaSource.DefaultId,
            "Band/Song.LRC",
            "Plain line",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadAsync_FolderNotWritable_StoresTheLyricsInTheDatabase()
    {
        _files.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.NotWritable);

        LyricsUploadResult result = await _uploader.UploadAsync(_trackId, "Plain line", false);

        result.Outcome.Should().Be(LyricsUploadOutcome.SavedToDatabase);
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download =>
                download.Status == LyricsDownloadStatus.Uploaded && download.Content == "Plain line"),
            Arg.Any<CancellationToken>());
    }

    [TestCase("")]
    [TestCase("  \n\n ")]
    [TestCase("[ar:Artist]\n[00:01.00]")]
    public async Task UploadAsync_NoLineWithText_SavesNothing(string text)
    {
        (await _uploader.UploadAsync(_trackId, text, true)).Outcome.Should().Be(LyricsUploadOutcome.Empty);

        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task UploadAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        (await _uploader.UploadAsync(Guid.NewGuid(), "Line", false)).Outcome
            .Should().Be(LyricsUploadOutcome.TrackNotFound);
    }

    [Test]
    public async Task UploadAsync_Publish_SendsTheLinesWithAndWithoutTimesAndTheNamesOfTheTrack()
    {
        LyricsUploadResult result = await _uploader.UploadAsync(_trackId, Synced, true);

        result.Publication.Should().Be(LyricsPublication.Published);
        await _lrclib.Received(1).PublishAsync(
            new LrclibPublication(
                "Song",
                "Band, Second feat. Guest",
                "Album",
                TimeSpan.FromSeconds(180),
                "One\n\nTwo",
                "[00:01.00]One\n[00:02.00]\n[00:03.00]Two"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadAsync_PublishPlainLyrics_SendsNoSynchronizedLyrics()
    {
        await _uploader.UploadAsync(_trackId, "One\nTwo", true);

        await _lrclib.Received(1).PublishAsync(
            Arg.Is<LrclibPublication>(publication =>
                publication.PlainLyrics == "One\nTwo" && publication.SyncedLyrics.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadAsync_PublishWithoutAlbumOrWhileSwitchedOff_KeepsTheLyricsAndPublishesNothing()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row() with { AlbumTitle = null });
        LyricsUploadResult withoutAlbum = await _uploader.UploadAsync(_trackId, Synced, true);

        _settings.Current.Returns(new LibrarySettings());
        LyricsUploadResult switchedOff = await _uploader.UploadAsync(_trackId, Synced, true);

        withoutAlbum.Should().Be(
            new LyricsUploadResult(LyricsUploadOutcome.SavedToFile, LyricsPublication.MissingMetadata));
        switchedOff.Should().Be(new LyricsUploadResult(LyricsUploadOutcome.SavedToFile, LyricsPublication.Disabled));
        await _lrclib.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [TestCase(LrclibPublishStatus.Rejected, LyricsPublication.Rejected)]
    [TestCase(LrclibPublishStatus.RateLimited, LyricsPublication.RateLimited)]
    [TestCase(LrclibPublishStatus.Unavailable, LyricsPublication.Unavailable)]
    public async Task UploadAsync_PublishingFails_KeepsTheLyrics(LrclibPublishStatus status, LyricsPublication expected)
    {
        _lrclib.PublishAsync(Arg.Any<LrclibPublication>(), Arg.Any<CancellationToken>())
            .Returns(new LrclibPublishResult(status));

        LyricsUploadResult result = await _uploader.UploadAsync(_trackId, Synced, true);

        result.Should().Be(new LyricsUploadResult(LyricsUploadOutcome.SavedToFile, expected));
    }

    [Test]
    public void PublicationOf_TrackWithoutArtist_TakesTheNamesOfTheFileName()
    {
        LyricsTrackRow row = Row() with
        {
            BaseName = "Kerli - Diamond Hard",
            Title = "Kerli - Diamond Hard",
            ArtistName = null,
            Artists = [],
        };

        LrclibPublication? publication = LyricsUploader.PublicationOf(row, LrcParser.Parse("Line"));

        publication!.TrackName.Should().Be("Diamond Hard");
        publication.ArtistName.Should().Be("Kerli");
    }

    private static LyricsTrackRow Row()
    {
        return new LyricsTrackRow(
            _trackId,
            MediaSource.DefaultId,
            "Band",
            "Song",
            "Song",
            "Band",
            "Album",
            TimeSpan.FromSeconds(180),
            null,
            "Band/Song.mp3",
            null,
            null)
        {
            Artists =
            [
                new LyricsArtistRow("Band", ArtistRole.Main),
                new LyricsArtistRow("Second", ArtistRole.Main),
                new LyricsArtistRow("Guest", ArtistRole.Featured),
            ],
        };
    }
}
