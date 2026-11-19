using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Library.Checks;
using WaveMP.Core.Library.Extensions;
using WaveMP.Core.Metadata.Extensions;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Extensions;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Library.Tests.Checks;

/// <summary>
/// Scans media created with ffmpeg into a real PostgreSQL database and checks the library: every kind of problem has a
/// file of its own.
/// </summary>
[TestFixture]
[Category("Integration")]
public class LibraryCheckDatabaseTests
{
    private const string DatabaseName = "wavemp_tests_library_check";

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
            "TRUNCATE covers, media_files, tracks, albums, artists, library_checks RESTART IDENTITY CASCADE");
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
    public async Task Check_AfterAScan_FindsEveryKindOfProblem()
    {
        // Complete: tags, album cover and a video; no problem at all.
        await AudioAsync("Covered/Album/01 Complete.mp3", Tags("Complete", "Covered", "Album"));
        await TestMediaGenerator.CreateVideoAsync(
            _media.Combine("Covered/Album/01 Complete.mkv"),
            VideoFormat.Mkv,
            160,
            120);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Covered/Album/cover.jpg"), 100, 100);
        // No album tag and no album folder: missing tag, unknown album, no cover, no video.
        await AudioAsync("Single.mp3", Tags("Single", "Lonely", null));
        // No tags at all; the album comes from the folders.
        await AudioAsync("Pathy/Folder Album/02 - Untagged.mp3", null);
        // The same song twice with nearly the same length, and a much longer live version.
        await AudioAsync("Dup/First/Same.mp3", Tags("Same", "Dup", "First"), durationSeconds: 2);
        await AudioAsync("Dup/Second/Same.flac", Tags("SAME ", "Dup", "Second"), AudioFormat.Flac, 3);
        await AudioAsync("Dup/Second/Same Live.mp3", Tags("Same", "Dup", "Second"), durationSeconds: 20);
        // A damaged file and a video without audio file.
        TestMediaGenerator.CreateCorruptFile(_media.Combine("Broken/Broken.mp3"));
        await TestMediaGenerator.CreateVideoAsync(
            _media.Combine("Clips/Clip.mkv"),
            VideoFormat.Mkv,
            160,
            120,
            new TestTags { Title = "Clip", Artist = "Director", Album = "Clips" });
        await ScanAsync();

        LibraryCheckJobResult result = await CheckAsync();

        result.TracksChecked.Should().Be(8);
        List<LibraryIssueRow> issues = await ListAsync();
        Describe(issues).Should().BeEquivalentTo(
        [
            (LibraryIssueType.MissingTags, "Single", "Album"),
            (LibraryIssueType.UnknownAlbum, "Single", string.Empty),
            (LibraryIssueType.MissingCover, "Single", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "Single", string.Empty),
            (LibraryIssueType.MissingTags, "02 - Untagged", "Title, Artist, Album"),
            (LibraryIssueType.MissingCover, "02 - Untagged", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "02 - Untagged", string.Empty),
            (LibraryIssueType.PossibleDuplicate, "Same", "Same"),
            (LibraryIssueType.MissingCover, "Same", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "Same", string.Empty),
            (LibraryIssueType.PossibleDuplicate, "Same", "Same"),
            (LibraryIssueType.MissingCover, "Same", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "Same", string.Empty),
            (LibraryIssueType.MissingCover, "Same Live", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "Same Live", string.Empty),
            (LibraryIssueType.UnreadableFile, "Broken", nameof(MediaFileIssue.ProbeFailed)),
            (LibraryIssueType.MissingTags, "Broken", "Title, Artist, Album"),
            (LibraryIssueType.UnknownAlbum, "Broken", string.Empty),
            (LibraryIssueType.MissingCover, "Broken", string.Empty),
            (LibraryIssueType.AudioWithoutVideo, "Broken", string.Empty),
            (LibraryIssueType.VideoWithoutAudio, "Clip", string.Empty),
            (LibraryIssueType.MissingCover, "Clip", string.Empty),
        ]);
        issues.Where(issue => issue.Type == LibraryIssueType.PossibleDuplicate)
            .Select(issue => (issue.Track!.DirectoryPath, issue.RelatedTrack!.DirectoryPath))
            .Should().BeEquivalentTo([("Dup/First", "Dup/Second"), ("Dup/Second", "Dup/First")]);
        issues.Single(issue => issue.Type == LibraryIssueType.UnreadableFile).File!.RelativePath
            .Should().Be("Broken/Broken.mp3");

        ILibraryIssueService service = _services.CreateScope().ServiceProvider
            .GetRequiredService<ILibraryIssueService>();
        LibraryIssueSummary summary = await service.GetSummaryAsync();
        summary.TrackCount.Should().Be(8);
        summary.Counts.Should().AllSatisfy(count => count.Count.Should().BePositive(
            "every kind of problem has a file ({0})",
            count.Type));
    }

    [Test]
    public async Task Check_AfterTheProblemIsFixed_RemovesIt()
    {
        await AudioAsync("Artist/Album/Song.mp3", Tags("Song", "Artist", "Album"));
        await ScanAsync();
        await CheckAsync();
        (await ListAsync()).Should().Contain(issue => issue.Type == LibraryIssueType.MissingCover);

        await TestMediaGenerator.CreateImageAsync(_media.Combine("Artist/Album/folder.jpg"), 100, 100);
        await ScanAsync();
        LibraryCheckJobResult result = await CheckAsync();

        result.Removed.Should().Be(1);
        (await ListAsync()).Select(issue => issue.Type).Should().Equal(LibraryIssueType.AudioWithoutVideo);
    }

    private static IEnumerable<(LibraryIssueType Type, string Name, string Detail)> Describe(
        IEnumerable<LibraryIssueRow> issues)
    {
        return issues.Select(issue => (
            issue.Type,
            issue.Track?.BaseName ?? issue.File!.RelativePath,
            issue.Type switch
            {
                LibraryIssueType.MissingTags => string.Join(", ", issue.MissingTags),
                LibraryIssueType.PossibleDuplicate => issue.RelatedTrack!.BaseName,
                LibraryIssueType.UnreadableFile => issue.FileIssue.ToString()!,
                _ => string.Empty,
            }));
    }

    private static TestTags? Tags(string title, string artist, string? album)
    {
        return new TestTags { Title = title, Artist = artist, Album = album };
    }

    private Task AudioAsync(
        string relativePath,
        TestTags? tags,
        AudioFormat format = AudioFormat.Mp3,
        int durationSeconds = 1)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(relativePath),
            format,
            tags,
            durationSeconds: durationSeconds);
    }

    private async Task ScanAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ILibraryScanner>().ScanAsync(ScanScope.Full, Guid.NewGuid());
    }

    private async Task<LibraryCheckJobResult> CheckAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILibraryCheckRunner>().RunAsync();
    }

    private async Task<List<LibraryIssueRow>> ListAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        ILibraryIssueService service = scope.ServiceProvider.GetRequiredService<ILibraryIssueService>();
        return [.. (await service.ListAsync(new LibraryIssueListRequest(0, 100))).Items];
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
        services.AddWaveMpLibrary();
        return services.BuildServiceProvider();
    }
}
