using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Metadata.Extensions;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Core.Settings.Extensions;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Scanning.Tests.Selections;

/// <summary>
/// Selects files of synthetic media created with ffmpeg in a library scanned into a real PostgreSQL database, and
/// scans again.
/// </summary>
[TestFixture]
[Category("Integration")]
public class FileSelectionServiceDatabaseTests
{
    private const string DatabaseName = "wavemp_tests_scanning_file_selection";

    private TestDatabase _database = null!;
    private TemporaryDirectory _media = null!;
    private ServiceProvider _services = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateMigratedAsync(DatabaseName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await using WaveMpDbContext dbContext = _database.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE track_file_selections, covers, media_files, tracks, albums, artists RESTART IDENTITY CASCADE");
        _media = new TemporaryDirectory();
        _services = CreateServices(_media.Path);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task SetAsync_SelectedFiles_AreUsedAtOnceAndStayAcrossRescans()
    {
        await CreateSongAsync();
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();

        FileSelectionResult result = await SetAsync(
            trackId,
            new FileChoices(files["Song.mp3"], files["Song.mp4"], files["Song.jpg"]));

        result.Status.Should().Be(FileSelectionStatus.Done);
        FileSelectionDetails details = result.Details!;
        details.Audio.Should().BeEquivalentTo(new
        {
            State = FileSelectionState.Selected,
            SelectedFileId = files["Song.mp3"],
            SelectedPath = "Band/Album/Song.mp3",
            ActiveFileId = files["Song.mp3"],
            AutomaticFileId = files["Song.flac"],
        });
        details.Audio.CandidateFileIds.Should().Equal(files["Song.flac"], files["Song.mp3"]);
        details.Video.ActiveFileId.Should().Be(files["Song.mp4"]);
        details.Video.AutomaticFileId.Should().Be(files["Song.mkv"]);
        details.Cover.ActiveFileId.Should().Be(files["Song.jpg"]);
        details.Cover.AutomaticFileId.Should().Be(files["Song.png"]);
        await ExpectSelectionAppliedAsync(trackId);

        // An unchanged folder is not written again; a changed file makes the scan write it.
        await ScanAsync();
        await TouchAsync("Band/Album/Song.flac");
        ScanResult rescan = await ScanAsync();

        rescan.Statistics.FilesUpdated.Should().Be(1);
        await ExpectSelectionAppliedAsync(trackId);
    }

    [Test]
    public async Task SetAsync_FileWithoutCompilationFlag_MovesTheSongOutOfTheCompilationAndBack()
    {
        TestTags hits = new() { Album = "Hits", Artist = "A" };
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Hits/Song.flac"),
            AudioFormat.Flac,
            hits with { Compilation = "1" });
        await TestMediaGenerator.CreateAudioAsync(_media.Combine("Hits/Song.mp3"), AudioFormat.Mp3, hits);
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();

        async Task ExpectAlbumAsync(string? artist, bool isCompilation)
        {
            await using WaveMpDbContext db = _database.CreateDbContext();
            Album album = await db.Albums.Include(item => item.AlbumArtist).SingleAsync();
            (album.AlbumArtist?.Name, album.IsCompilation).Should().Be((artist, isCompilation));
        }

        await ExpectAlbumAsync(null, true);

        await SetAsync(trackId, new FileChoices(files["Song.mp3"]));
        await ExpectAlbumAsync("A", false);

        await SetAsync(trackId, FileChoices.None);
        await ExpectAlbumAsync(null, true);
    }

    [Test]
    public async Task Scan_SelectedFileDeleted_FallsBackVisiblyAndSelectsItAgainWhenItIsBack()
    {
        await CreateSongAsync();
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();
        await SetAsync(trackId, new FileChoices(files["Song.mp3"]));

        File.Delete(_media.Combine("Band/Album/Song.mp3"));
        await ScanAsync();

        FileSelectionDetails missing = (await GetAsync(trackId))!;
        missing.Audio.State.Should().Be(FileSelectionState.Missing);
        missing.Audio.SelectedPath.Should().Be("Band/Album/Song.mp3");
        missing.Audio.ActiveFileId.Should().Be(files["Song.flac"]);
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            (await db.TrackFileSelections.SingleAsync()).IsMissing.Should().BeTrue();
            (await db.Tracks.SingleAsync()).Title.Should().Be("Lossless");
        }

        Page<TrackFileSummaryRow> page =
            await ListAsync(new FileSelectionListRequest(0, 10, Filter: FileSelectionFilter.Missing));
        page.Items.Should().ContainSingle().Which.Audio.Should().Be(FileSelectionState.Missing);

        // Sending the missing selection again keeps it.
        FileSelectionResult kept = await SetAsync(trackId, new FileChoices(files["Song.mp3"], files["Song.mkv"]));
        kept.Details!.Audio.State.Should().Be(FileSelectionState.Missing);
        kept.Details.Video.State.Should().Be(FileSelectionState.Selected);

        await AudioAsync("Band/Album/Song.mp3", AudioFormat.Mp3, "Lossy");
        await ScanAsync();

        FileSelectionDetails back = (await GetAsync(trackId))!;
        back.Audio.State.Should().Be(FileSelectionState.Selected);
        back.Audio.ActiveFileId.Should().Be(back.Audio.SelectedFileId);
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            (await db.Tracks.SingleAsync()).Title.Should().Be("Lossy");
            (await db.TrackFileSelections.AnyAsync(selection => selection.IsMissing)).Should().BeFalse();
        }
    }

    [Test]
    public async Task SetAsync_FileOfAnotherCategoryOrTrack_IsInvalidAndChangesNothing()
    {
        await CreateSongAsync();
        await AudioAsync("Band/Album/Other.mp3", AudioFormat.Mp3, "Other");
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();

        FileSelectionResult result = await SetAsync(
            trackId,
            new FileChoices(files["Song.mkv"], files["Song.mp3"], files["Other.mp3"]));

        result.Status.Should().Be(FileSelectionStatus.Invalid);
        result.InvalidCategories.Should().Equal(
            FileSelectionCategory.Audio,
            FileSelectionCategory.Video,
            FileSelectionCategory.Cover);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.TrackFileSelections.AnyAsync()).Should().BeFalse();
        (await SetAsync(Guid.NewGuid(), FileChoices.None)).Status.Should().Be(FileSelectionStatus.TrackNotFound);
    }

    [Test]
    public async Task ResetAsync_Selection_ReturnsToTheQualityRule()
    {
        await CreateSongAsync();
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();
        await SetAsync(trackId, new FileChoices(files["Song.mp3"], files["Song.mp4"], files["Song.jpg"]));

        bool reset;
        await using (AsyncServiceScope scope = _services.CreateAsyncScope())
        {
            reset = await scope.ServiceProvider.GetRequiredService<IFileSelectionService>().ResetAsync(trackId);
        }

        reset.Should().BeTrue();
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.TrackFileSelections.AnyAsync()).Should().BeFalse();
        (await db.MediaFiles.Where(file => file.IsPrimary).Select(file => file.FileName).ToListAsync())
            .Should().BeEquivalentTo("Song.flac", "Song.mkv");
        (await db.Covers.Where(cover => cover.IsPrimary).Select(cover => cover.MediaFile!.FileName).SingleAsync())
            .Should().Be("Song.png");
        (await db.Tracks.SingleAsync()).Title.Should().Be("Lossless");
    }

    [Test]
    public async Task ListAsync_Filters_FindTheTracksWithChoicesAndSelections()
    {
        await CreateSongAsync();
        await AudioAsync("Band/Album/Single.mp3", AudioFormat.Mp3, "Single");
        await ScanAsync();
        Guid trackId = await TrackIdAsync();
        Dictionary<string, Guid> files = await FileIdsAsync();
        await SetAsync(trackId, new FileChoices(Video: files["Song.mp4"]));

        Page<TrackFileSummaryRow> all = await ListAsync(new FileSelectionListRequest(0, 10));
        all.Items.Select(row => row.BaseName).Should().Equal("Single", "Song");
        all.Items[1].Should().BeEquivalentTo(new
        {
            Title = "Lossless",
            AudioCount = 2,
            VideoCount = 2,
            CoverCount = 2,
            Audio = FileSelectionState.Automatic,
            Video = FileSelectionState.Selected,
            Cover = FileSelectionState.Automatic,
        });

        foreach (FileSelectionFilter filter in (FileSelectionFilter[])
            [
                FileSelectionFilter.SeveralAudio,
                FileSelectionFilter.SeveralVideo,
                FileSelectionFilter.SeveralCovers,
                FileSelectionFilter.Choices,
                FileSelectionFilter.Selected,
            ])
        {
            (await ListAsync(new FileSelectionListRequest(0, 10, Filter: filter))).Items
                .Should().ContainSingle(filter.ToString()).Which.TrackId.Should().Be(trackId);
        }

        (await ListAsync(new FileSelectionListRequest(0, 10, Filter: FileSelectionFilter.Missing))).Total
            .Should().Be(0);
        (await ListAsync(new FileSelectionListRequest(0, 10, Search: "sing"))).Items
            .Should().ContainSingle().Which.BaseName.Should().Be("Single");
        (await ListAsync(new FileSelectionListRequest(0, 10, FileSelectionSortField.FileCount))).Items
            .Select(row => row.BaseName).Should().Equal("Song", "Single");
    }

    private async Task ExpectSelectionAppliedAsync(Guid trackId)
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.MediaFiles.Where(file => file.IsPrimary).Select(file => file.FileName).ToListAsync())
            .Should().BeEquivalentTo("Song.mp3", "Song.mp4");
        (await db.Covers
                .Where(cover => cover.TrackId == trackId && cover.IsPrimary)
                .Select(cover => cover.MediaFile!.FileName)
                .SingleAsync())
            .Should().Be("Song.jpg");
        Track track = await db.Tracks.SingleAsync(item => item.Id == trackId);
        track.Title.Should().Be("Lossy");
        (await db.TrackFileSelections.CountAsync(selection => !selection.IsMissing)).Should().Be(3);
    }

    /// <summary>
    /// A track with two audio files (the FLAC wins by quality), two videos (the larger one wins) and two pictures
    /// (the larger one wins). The tags tell which audio file the metadata comes from.
    /// </summary>
    private async Task CreateSongAsync()
    {
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await AudioAsync("Band/Album/Song.flac", AudioFormat.Flac, "Lossless");
        await AudioAsync("Band/Album/Song.mp3", AudioFormat.Mp3, "Lossy");
        await TestMediaGenerator.CreateVideoAsync(_media.Combine("Band/Album/Song.mkv"), VideoFormat.Mkv, 640, 480);
        await TestMediaGenerator.CreateVideoAsync(_media.Combine("Band/Album/Song.mp4"), VideoFormat.Mp4, 320, 240);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/Song.jpg"), 100, 100);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/Song.png"), 300, 300);
    }

    private Task AudioAsync(string relativePath, AudioFormat format, string title)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(relativePath),
            format,
            new TestTags { Title = title, Artist = "Band", Album = "Album" });
    }

    /// <summary>
    /// Gives the file a later change time, so the scan reads it and writes its folder again.
    /// </summary>
    private Task TouchAsync(string relativePath)
    {
        File.SetLastWriteTimeUtc(_media.Combine(relativePath), DateTime.UtcNow.AddMinutes(1));
        return Task.CompletedTask;
    }

    private async Task<Guid> TrackIdAsync()
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        return await db.Tracks.Where(track => track.BaseName == "Song").Select(track => track.Id).SingleAsync();
    }

    private async Task<Dictionary<string, Guid>> FileIdsAsync()
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        return await db.MediaFiles.ToDictionaryAsync(file => file.FileName, file => file.Id);
    }

    private async Task<ScanResult> ScanAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILibraryScanner>()
            .ScanAsync(ScanScope.Full, Guid.NewGuid());
    }

    private async Task<FileSelectionResult> SetAsync(Guid trackId, FileChoices choices)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IFileSelectionService>().SetAsync(trackId, choices);
    }

    private async Task<FileSelectionDetails?> GetAsync(Guid trackId)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IFileSelectionService>().GetAsync(trackId);
    }

    private async Task<Page<TrackFileSummaryRow>> ListAsync(FileSelectionListRequest request)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IFileSelectionService>().ListAsync(request);
    }

    private static ServiceProvider CreateServices(string mediaPath)
    {
        Dictionary<string, string?> settings = TestDatabaseSettings.ToConfiguration(DatabaseName);
        settings["MEDIA_PATH"] = mediaPath;
        settings["Metadata:FfprobePath"] = FfmpegTool.FfprobePath;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddWaveMpData();
        services.AddWaveMpSettings();
        services.AddWaveMpMetadata();
        services.AddWaveMpJobs();
        services.AddWaveMpScanning();
        return services.BuildServiceProvider();
    }
}
