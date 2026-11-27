using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Services;

[TestFixture]
public class LyricsServiceTests
{
    private static readonly Guid _trackId = Guid.NewGuid();

    private ILyricsRepository _repository = null!;
    private ILyricsFileStore _files = null!;
    private ILyricsTagReader _tags = null!;
    private LyricsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _files = Substitute.For<ILyricsFileStore>();
        _tags = Substitute.For<ILyricsTagReader>();
        IMediaFileLocator locator = Substitute.For<IMediaFileLocator>();
        locator.TryGetFullPath(MediaSource.DefaultId, Arg.Any<string>(), out Arg.Any<string>())
            .Returns(call =>
            {
                call[2] = "/media/" + call.ArgAt<string>(1);
                return true;
            });
        _service = new LyricsService(_repository, _files, _tags, locator);
    }

    [Test]
    public async Task GetAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        LyricsResult result = await _service.GetAsync(_trackId);

        result.Status.Should().Be(LyricsStatus.TrackNotFound);
    }

    [Test]
    public async Task GetAsync_TrackWithoutLyrics_ReturnsNoLyrics()
    {
        Track(Row());

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Status.Should().Be(LyricsStatus.NoLyrics);
    }

    [Test]
    public async Task GetAsync_LyricsFile_WinsOverAllOtherSources()
    {
        Track(Row(lyricsFile: "Band/Song.lrc", download: Stored("[00:01.00]Downloaded")));
        _files.ReadAsync(MediaSource.DefaultId, "Band/Song.lrc", Arg.Any<CancellationToken>()).Returns(
            "[00:01.00]From file");
        _tags.Read(Arg.Any<string>()).Returns(new TagLyrics([], "From tags"));

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.Source.Should().Be(LyricsSource.File);
        result.Lyrics.Document.Lines.Should().ContainSingle().Which.Text.Should().Be("From file");
    }

    [Test]
    public async Task GetAsync_EmptyOrUnreadableLyricsFile_FallsBackToTheDownload()
    {
        Track(Row(lyricsFile: "Band/Song.lrc", download: Stored("Downloaded line")));
        _files.ReadAsync(MediaSource.DefaultId, "Band/Song.lrc", Arg.Any<CancellationToken>()).Returns("[ar:Artist]");

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.Source.Should().Be(LyricsSource.Download);
        result.Lyrics.Document.Type.Should().Be(LyricsType.Static);
    }

    [Test]
    public async Task GetAsync_UploadedLyricsInTheDatabase_ComeFromTheUpload()
    {
        Track(Row(download: new LyricsDownloadRow(LyricsDownloadStatus.Uploaded, "Own line", DateTimeOffset.UtcNow)));

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.Source.Should().Be(LyricsSource.Upload);
        result.Lyrics.OtherVersion.Should().BeFalse();
    }

    [Test]
    public async Task GetAsync_PlainLyricsOfAnotherVersion_AreMarked()
    {
        Track(Row(
            lyricsFile: "Band/Song.lrc",
            download: new LyricsDownloadRow(LyricsDownloadStatus.SavedToFile, null, DateTimeOffset.UtcNow, true)));
        _files.ReadAsync(MediaSource.DefaultId, "Band/Song.lrc", Arg.Any<CancellationToken>()).Returns("Plain line");

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.OtherVersion.Should().BeTrue();
    }

    [Test]
    public async Task GetAsync_SynchronizedFileWrittenAfterTheMark_IsNoOtherVersion()
    {
        Track(Row(
            lyricsFile: "Band/Song.lrc",
            download: new LyricsDownloadRow(LyricsDownloadStatus.SavedToFile, null, DateTimeOffset.UtcNow, true)));
        _files.ReadAsync(MediaSource.DefaultId, "Band/Song.lrc", Arg.Any<CancellationToken>())
            .Returns("[00:01.00]Synced line");

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.OtherVersion.Should().BeFalse();
    }

    [Test]
    public async Task GetAsync_DownloadWithoutContent_FallsBackToTheTags()
    {
        Track(Row(download: new LyricsDownloadRow(LyricsDownloadStatus.SavedToFile, null, DateTimeOffset.UtcNow)));
        _tags.Read("/media/Band/Song.mp3").Returns(new TagLyrics([new TimedLyricsText(TimeSpan.Zero, "Tag")], null));

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.Source.Should().Be(LyricsSource.Tags);
        result.Lyrics.Document.Type.Should().Be(LyricsType.LineSynchronized);
    }

    [Test]
    public async Task GetAsync_AudioWithoutLyrics_ReadsTheTagsOfTheVideo()
    {
        Track(Row(video: "Band/Song.mkv"));
        _tags.Read("/media/Band/Song.mkv").Returns(new TagLyrics([], "Video lyrics"));

        LyricsResult result = await _service.GetAsync(_trackId);

        result.Lyrics!.Source.Should().Be(LyricsSource.Tags);
        result.Lyrics.Document.Lines.Should().ContainSingle().Which.Text.Should().Be("Video lyrics");
    }

    private void Track(LyricsTrackRow row)
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(row);
    }

    private static LyricsDownloadRow Stored(string content)
    {
        return new LyricsDownloadRow(LyricsDownloadStatus.SavedToDatabase, content, DateTimeOffset.UtcNow);
    }

    private static LyricsTrackRow Row(
        string? lyricsFile = null,
        string? video = null,
        LyricsDownloadRow? download = null)
    {
        return new LyricsTrackRow(
            _trackId,
            MediaSource.DefaultId,
            "Band",
            "Song",
            "Song",
            "Band",
            "Album",
            TimeSpan.FromMinutes(3),
            lyricsFile,
            "Band/Song.mp3",
            video,
            download);
    }
}
