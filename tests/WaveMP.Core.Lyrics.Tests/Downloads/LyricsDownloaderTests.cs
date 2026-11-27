using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Saving;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

/// <summary>
/// The download with a simulated LRCLIB (substituted client); no request leaves the process.
/// </summary>
[TestFixture]
public class LyricsDownloaderTests
{
    private static readonly Guid _trackId = Guid.NewGuid();
    private static readonly LrclibRecord _synced = new()
    {
        Id = 1,
        AlbumName = "Album",
        Duration = 180,
        SyncedLyrics = "[00:01.00]Synced",
        PlainLyrics = "Plain",
    };

    private ILyricsRepository _repository = null!;
    private ILyricsService _lyrics = null!;
    private ILrclibClient _lrclib = null!;
    private ILyricsFileStore _files = null!;
    private ILibraryScanner _scanner = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private LyricsDownloader _downloader = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row());
        _repository.SaveDownloadAsync(Arg.Any<LyricsDownload>(), Arg.Any<CancellationToken>()).Returns(true);
        _lyrics = Substitute.For<ILyricsService>();
        _lyrics.GetAsync(_trackId, Arg.Any<CancellationToken>()).Returns(LyricsResult.NoLyrics);
        _lrclib = Substitute.For<ILrclibClient>();
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _lrclib.SearchTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _lrclib.GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _files = Substitute.For<ILyricsFileStore>();
        _files.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.Created);
        _files.ReplaceAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.Created);
        _scanner = Substitute.For<ILibraryScanner>();
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings { DownloadLyrics = true });
        _downloader = new LyricsDownloader(
            _repository,
            _lyrics,
            _lrclib,
            new LyricsSaver(_repository, _files, _scanner, TimeProvider.System, NullLogger<LyricsSaver>.Instance),
            _settings);
    }

    [Test]
    public async Task DownloadAsync_SwitchedOff_AsksNobody()
    {
        _settings.Current.Returns(new LibrarySettings());

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.Disabled);
        await _lrclib.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
        await _lrclib.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        LyricsDownloadResult result = await _downloader.DownloadAsync(Guid.NewGuid(), LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.TrackNotFound);
    }

    [Test]
    public async Task DownloadAsync_TrackWithLyricsFile_KeepsIt()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row(lyricsFile: "Band/Song.lrc"));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.LyricsFileExists);
        await _lrclib.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_AutomaticForTrackWithTagLyrics_RecordsThatNoneAreNeeded()
    {
        _lyrics.GetAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(LyricsResult.Found(new TrackLyrics(LyricsDocument.Empty, LyricsSource.Tags)));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.NotNeeded);
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.NotNeeded),
            Arg.Any<CancellationToken>());
        await _lrclib.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_ManualForTrackWithTagLyrics_DownloadsAnyway()
    {
        _lyrics.GetAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(LyricsResult.Found(new TrackLyrics(LyricsDocument.Empty, LyricsSource.Tags)));
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
    }

    [Test]
    public async Task DownloadAsync_ExactMatch_SavesTheSyncedLyricsAsFileAndReadsTheFolderAgain()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _lrclib.Received(1).GetAsync(
            new LrclibQuery("Song", "Band", "Album", TimeSpan.FromSeconds(180)),
            Arg.Any<CancellationToken>());
        await _lrclib.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
        await _files.Received(1).CreateAsync(
            MediaSource.DefaultId,
            "Band/Song.lrc",
            "[00:01.00]Synced",
            Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download =>
                download.TrackId == _trackId
                && download.Status == LyricsDownloadStatus.SavedToFile
                && download.Content == null),
            Arg.Any<CancellationToken>());
        await _scanner.Received(1).RefreshFolderAsync(
            MediaSource.DefaultId,
            "Band",
            Arg.Is<IReadOnlyCollection<string>>(files => files.SequenceEqual(new[] { "Band/Song.lrc" })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_AutomaticForTrackWhoseTagsLostTheirLyrics_DownloadsThemAndReplacesTheResult()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row() with
        {
            Download = new LyricsDownloadRow(LyricsDownloadStatus.NotNeeded, null, DateTimeOffset.UnixEpoch),
        });
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _files.Received(1).CreateAsync(
            MediaSource.DefaultId,
            "Band/Song.lrc",
            "[00:01.00]Synced",
            Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.SavedToFile),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_NoExactMatch_SearchesAndTakesTheBestRecord()
    {
        LrclibRecord plain = _synced with { Id = 2, SyncedLyrics = null };
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([plain]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _files.Received(1).CreateAsync(
            MediaSource.DefaultId,
            "Band/Song.lrc",
            "Plain",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_IncompleteTrack_OnlySearches()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row(album: null));
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _lrclib.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_TrackInTheMediaFolderItself_SavesTheFileThere()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row(directory: string.Empty));
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        await _files.Received(1).CreateAsync(
            MediaSource.DefaultId,
            "Song.lrc",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_FolderNotWritable_StoresTheLyricsInTheDatabase()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));
        _files.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.NotWritable);

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToDatabase);
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download =>
                download.Status == LyricsDownloadStatus.SavedToDatabase && download.Content == "[00:01.00]Synced"),
            Arg.Any<CancellationToken>());
        await _scanner.DidNotReceiveWithAnyArgs().RefreshFolderAsync(default, default!, default!, default);
    }

    [Test]
    public async Task DownloadAsync_FileCreatedInTheMeantime_KeepsIt()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));
        _files.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.Exists);

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.LyricsFileExists);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_ReadingTheFolderFails_StillSavesTheLyrics()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));
        _scanner.RefreshFolderAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("ffprobe missing"));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
    }

    [Test]
    public async Task DownloadAsync_NothingFound_RecordsTheLookup()
    {
        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.NotFound);
        await _lrclib.Received(1).SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.NotFound),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_InstrumentalTrack_RecordsIt()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                LrclibResponse.Found([_synced with { Instrumental = true, SyncedLyrics = null, PlainLyrics = null }]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.Instrumental);
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.Instrumental),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_RateLimited_ReturnsTheWaitAndRecordsNothing()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.RateLimited(TimeSpan.FromSeconds(9)));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Should().Be(new LyricsDownloadResult(LyricsDownloadOutcome.RateLimited, TimeSpan.FromSeconds(9)));
        result.IsTransient.Should().BeTrue();
        await _lrclib.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_NoInternetConnection_ReturnsUnavailableAndRecordsNothing()
    {
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.Unavailable);

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.Unavailable);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_TrackRemovedWhileDownloading_ReturnsTrackNotFound()
    {
        _repository.SaveDownloadAsync(Arg.Any<LyricsDownload>(), Arg.Any<CancellationToken>()).Returns(false);

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.TrackNotFound);
    }

    [Test]
    public async Task DownloadAsync_OnlyAnotherVersionWithTheNames_SavesItsPlainLyricsMarked()
    {
        // The single is 4:02, the music video 5:43.
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row() with
        {
            Duration = TimeSpan.FromSeconds(343),
        });
        LrclibRecord single = new()
        {
            Id = 5,
            TrackName = "Song",
            ArtistName = "Band",
            AlbumName = "Single",
            Duration = 242,
            SyncedLyrics = "[00:01.00]Synced",
            PlainLyrics = "Plain",
        };
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([single]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        result.OtherVersion.Should().BeTrue();
        await _files.Received(1)
            .CreateAsync(MediaSource.DefaultId, "Band/Song.lrc", "Plain", Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveDownloadAsync(
            Arg.Is<LyricsDownload>(download => download.Status == LyricsDownloadStatus.SavedToFile
                && download.OtherVersion
                && download.MatcherVersion == LrclibMatcher.Version),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_AnotherSongOfAnotherDuration_IsNotTaken()
    {
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced with { TrackName = "Other", ArtistName = "Band", Duration = 300 }]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.NotFound);
    }

    [Test]
    public async Task DownloadAsync_TitleWithAdditions_SearchesAlsoWithoutThemAndWithTheMainArtist()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row() with
        {
            Title = "Song (Remastered 2011)",
            ArtistName = "Band feat. Guest",
        });
        _lrclib.SearchAsync(
                new LrclibQuery("Song", "Band", "Album", TimeSpan.FromSeconds(180)),
                Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _lrclib.Received(1).GetAsync(
            new LrclibQuery("Song (Remastered 2011)", "Band feat. Guest", "Album", TimeSpan.FromSeconds(180)),
            Arg.Any<CancellationToken>());
        await _lrclib.Received(2).SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>());
        await _lrclib.DidNotReceiveWithAnyArgs().SearchTextAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_FieldSearchFindsNothing_TakesTracksOfTheFreeTextSearchWithTheNames()
    {
        LrclibRecord unrelated = _synced with { Id = 8, TrackName = "Unrelated", ArtistName = "Band" };
        LrclibRecord named = _synced with { Id = 9, TrackName = "Song", ArtistName = "Band" };
        _lrclib.SearchTextAsync("Band Song", Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([unrelated, named]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        result.OtherVersion.Should().BeFalse();
        await _files.Received(1).CreateAsync(
            MediaSource.DefaultId,
            "Band/Song.lrc",
            "[00:01.00]Synced",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadAsync_FreeTextSearchWithOtherNamesOnly_FindsNothing()
    {
        _lrclib.SearchTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced with { TrackName = "Unrelated", ArtistName = "Someone" }]));

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        result.Outcome.Should().Be(LyricsDownloadOutcome.NotFound);
    }

    [Test]
    public async Task DownloadAsync_FreeTextSearchUnavailable_ReturnsUnavailable()
    {
        _lrclib.SearchTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.Unavailable);

        LyricsDownloadResult result = await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic);

        result.Outcome.Should().Be(LyricsDownloadOutcome.Unavailable);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task DownloadAsync_TrackWithoutArtist_SearchesWithTheArtistOfTheFileName()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row() with
        {
            BaseName = "Kerli - Diamond Hard",
            Title = "Kerli - Diamond Hard",
            ArtistName = null,
            AlbumTitle = null,
        });

        await _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Manual);

        await _lrclib.Received(1).SearchAsync(
            new LrclibQuery("Diamond Hard", "Kerli", null, TimeSpan.FromSeconds(180)),
            Arg.Any<CancellationToken>());
        await _lrclib.Received(1).SearchTextAsync("Kerli Diamond Hard", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadMatchAsync_FittingTrack_ReplacesTheLyricsFileWithTheSyncedLyrics()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row(lyricsFile: "Band/Song.lrc"));
        _lrclib.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(LrclibResponse.Found([_synced]));

        LyricsDownloadResult result = await _downloader.DownloadMatchAsync(_trackId, 1);

        result.Outcome.Should().Be(LyricsDownloadOutcome.SavedToFile);
        await _files.Received(1).ReplaceAsync(
            MediaSource.DefaultId,
            "Band/Song.lrc",
            "[00:01.00]Synced",
            Arg.Any<CancellationToken>());
        await _files.DidNotReceiveWithAnyArgs().CreateAsync(default, default!, default!, default);
    }

    [Test]
    public async Task DownloadMatchAsync_TrackOfAnotherDuration_SavesThePlainLyricsMarked()
    {
        _lrclib.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([_synced with { Duration = 300 }]));

        LyricsDownloadResult result = await _downloader.DownloadMatchAsync(_trackId, 1);

        result.OtherVersion.Should().BeTrue();
        await _files.Received(1)
            .CreateAsync(MediaSource.DefaultId, "Band/Song.lrc", "Plain", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DownloadMatchAsync_FileNotReplaceable_ReturnsFileNotWritable()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Row(lyricsFile: "Band/Song.lrc"));
        _lrclib.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(LrclibResponse.Found([_synced]));
        _files.ReplaceAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LyricsFileWriteStatus.NotWritable);

        LyricsDownloadResult result = await _downloader.DownloadMatchAsync(_trackId, 1);

        result.Outcome.Should().Be(LyricsDownloadOutcome.FileNotWritable);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    [Test]
    public async Task DownloadMatchAsync_UnknownMatchSwitchedOffOrServiceBusy_SavesNothing()
    {
        (await _downloader.DownloadMatchAsync(_trackId, 1)).Outcome.Should().Be(LyricsDownloadOutcome.NotFound);

        _lrclib.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(LrclibResponse.RateLimited(null));
        (await _downloader.DownloadMatchAsync(_trackId, 2)).Outcome.Should().Be(LyricsDownloadOutcome.RateLimited);

        _settings.Current.Returns(new LibrarySettings());
        (await _downloader.DownloadMatchAsync(_trackId, 1)).Outcome.Should().Be(LyricsDownloadOutcome.Disabled);
        await _repository.DidNotReceiveWithAnyArgs().SaveDownloadAsync(default!, default);
    }

    private static LyricsTrackRow Row(string? lyricsFile = null, string? album = "Album", string directory = "Band")
    {
        return new LyricsTrackRow(
            _trackId,
            MediaSource.DefaultId,
            directory,
            "Song",
            "Song",
            "Band",
            album,
            TimeSpan.FromSeconds(180),
            lyricsFile,
            $"{directory}/Song.mp3",
            null,
            null);
    }
}
