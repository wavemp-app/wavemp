using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Metadata.Extensions;
using WaveMP.Core.Organizing.Extensions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Extensions;
using WaveMP.Data;
using WaveMP.Data.Extensions;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Organizing.Tests.Databases;

/// <summary>
/// A library of synthetic media created with ffmpeg in a temporary folder, scanned into a real PostgreSQL database,
/// with the organizing services. The clock and the write probe can be replaced.
/// </summary>
[Category("Integration")]
public abstract class OrganizingDatabaseTestBase
{
    private TestDatabase _database = null!;

    protected abstract string DatabaseName { get; }

    protected TemporaryDirectory Media { get; private set; } = null!;

    protected TemporaryDirectory Data { get; private set; } = null!;

    protected ServiceProvider Services { get; private set; } = null!;

    protected DateTimeOffset Now { get; set; } = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The write probe; writable by default.
    /// </summary>
    protected IMediaWriteProbe WriteProbe { get; } = Substitute.For<IMediaWriteProbe>();

    protected virtual IDictionary<string, string?> ExtraSettings { get; } = new Dictionary<string, string?>();

    [OneTimeSetUp]
    public async Task OneTimeSetUpDatabase()
    {
        _database = await TestDatabase.CreateMigratedAsync(DatabaseName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownDatabase()
    {
        await _database.DisposeAsync();
    }

    [SetUp]
    public async Task SetUpLibrary()
    {
        await using (WaveMpDbContext dbContext = _database.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE organize_operations, own_albums, tag_backups, track_file_selections, covers, media_files, "
                + "tracks, albums, artists RESTART IDENTITY CASCADE");
        }

        Media = new TemporaryDirectory();
        Data = new TemporaryDirectory();
        WriteProbe.ClearReceivedCalls();
        WriteProbe.Probe(Arg.Any<string>())
            .Returns(call => Directory.Exists(call.Arg<string>()) ? MediaFolderState.Writable : MediaFolderState.Missing);
        Services = CreateServices();
        await Services.InitializeWaveMpOrganizingAsync();
    }

    [TearDown]
    public async Task TearDownLibrary()
    {
        await Services.DisposeAsync();
        Media.Dispose();
        Data.Dispose();
    }

    protected WaveMpDbContext CreateDbContext()
    {
        return _database.CreateDbContext();
    }

    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected Task AudioAsync(string relativePath, TestTags? tags, AudioFormat format = AudioFormat.Mp3)
    {
        return TestMediaGenerator.CreateAudioAsync(Media.Combine(relativePath), format, tags);
    }

    protected Task VideoAsync(string relativePath, TestTags? tags = null)
    {
        return TestMediaGenerator.CreateVideoAsync(Media.Combine(relativePath), VideoFormat.Mkv, 64, 48, tags);
    }

    protected async Task ScanAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ILibraryScanner>().ScanAsync(ScanScope.Full, Guid.NewGuid());
    }

    protected async Task<Guid> TrackIdAsync(string title)
    {
        await using WaveMpDbContext db = CreateDbContext();
        return await db.Tracks.Where(track => track.Title == title).Select(track => track.Id).SingleAsync();
    }

    /// <summary>
    /// The relative paths and last write times of every file below the media folder, to prove a preview changes
    /// nothing.
    /// </summary>
    protected IReadOnlyList<string> Snapshot()
    {
        return
        [
            .. Directory.EnumerateFileSystemEntries(Media.Path, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Media.Path, path).Replace('\\', '/')
                    + "|" + (File.Exists(path) ? new FileInfo(path).Length + "|" + File.GetLastWriteTimeUtc(path).Ticks : "dir"))
                .Order(StringComparer.Ordinal),
        ];
    }

    private ServiceProvider CreateServices()
    {
        Dictionary<string, string?> settings = TestDatabaseSettings.ToConfiguration(DatabaseName);
        settings["MEDIA_PATH"] = Media.Path;
        settings["DATA_PATH"] = Data.Path;
        settings["Metadata:FfprobePath"] = FfmpegTool.FfprobePath;
        foreach ((string key, string? value) in ExtraSettings)
        {
            settings[key] = value;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new TestClock(() => Now));
        services.AddWaveMpData();
        services.AddWaveMpSettings();
        services.AddWaveMpMetadata();
        services.AddWaveMpJobs();
        services.AddWaveMpScanning();
        services.AddWaveMpOrganizing();
        services.AddSingleton(WriteProbe);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The system clock with a time the test sets; timers keep working.
    /// </summary>
    private sealed class TestClock(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now();
        }
    }
}
