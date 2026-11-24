using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Extensions;
using WaveMP.Core.Organizing.Extensions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Extensions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Services;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Organizing.Tests.Databases;

/// <summary>
/// A library of synthetic media created with ffmpeg in a temporary folder, scanned into a real PostgreSQL database,
/// with the organizing services. The clock and the write probe can be replaced. Jobs are not run by a scheduler: the
/// requested runs are recorded and <see cref="RunOrganizeJobAsync"/> runs the organize job in the test.
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

    /// <summary>
    /// The job service; every start succeeds and is recorded in <see cref="JobRequests"/>.
    /// </summary>
    protected IJobService Jobs { get; } = Substitute.For<IJobService>();

    /// <summary>
    /// The runs requested since the test started.
    /// </summary>
    protected List<JobRunRequest> JobRequests { get; } = [];

    /// <summary>
    /// Whether a requested start of the organize job is rejected as already active.
    /// </summary>
    protected bool RejectOrganizeJob { get; set; }

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
        JobRequests.Clear();
        RejectOrganizeJob = false;
        Jobs.ClearReceivedCalls();
        Jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            JobRunRequest request = call.Arg<JobRunRequest>();
            if (RejectOrganizeJob && request.JobId == JobIds.LibraryOrganize)
            {
                return JobRunStartResult.Failure(JobRunStartError.AlreadyActive);
            }

            JobRequests.Add(request);
            return JobRunStartResult.Success(new JobRun
            {
                Id = Guid.CreateVersion7(),
                JobId = request.JobId,
                Trigger = request.Trigger,
                Status = WaveMP.Data.Entities.JobRunStatus.Queued,
                QueuedAt = Now,
            });
        });
        WriteProbe.Probe(Arg.Any<string>())
            .Returns(call => Directory.Exists(call.Arg<string>()) ? MediaFolderState.Writable : MediaFolderState.Missing);
        Services = CreateServices();
        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            // The settings are stored in the database: every test starts with the default layout template.
            await scope.ServiceProvider.GetRequiredService<IOrganizingSettingsService>().UpdateAsync(
                new UpdateOrganizingSettingsCommand(OrganizingSettings.DefaultLayoutTemplate));
        }

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

    /// <summary>
    /// Runs the organize job once for every run requested since the last call, in order, like the scheduler would.
    /// Returns the results.
    /// </summary>
    protected async Task<IReadOnlyList<LibraryOrganizeResult>> RunOrganizeJobAsync()
    {
        List<JobRunRequest> requests = [.. JobRequests.Where(request => request.JobId == JobIds.LibraryOrganize)];
        JobRequests.RemoveAll(request => request.JobId == JobIds.LibraryOrganize);
        List<LibraryOrganizeResult> results = [];
        foreach (JobRunRequest request in requests)
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            LibraryOrganizeJob job = ActivatorUtilities.CreateInstance<LibraryOrganizeJob>(scope.ServiceProvider);
            JobDataMap data = [];
            if (request.Payload is not null)
            {
                data[JobDataKeys.Payload] = request.Payload;
            }

            IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
            context.MergedJobDataMap.Returns(data);
            context.FireInstanceId.Returns(Guid.NewGuid().ToString());
            await job.Execute(context, CancellationToken.None);
            results.Add((LibraryOrganizeResult)context.Result!);
        }

        return results;
    }

    /// <summary>
    /// Previews the reorganization of the whole library: the operation is returned while planning, the organize job
    /// plans it. Returns the planned operation.
    /// </summary>
    protected async Task<OrganizeOperation> LibraryPreviewAsync()
    {
        OrganizePreviewResult result = await InScopeAsync(services =>
            services.GetRequiredService<IOrganizePreviewService>().PreviewLibraryAsync(null, null));
        result.Status.Should().Be(OrganizePreviewStatus.Created);
        result.Operation!.Status.Should().Be(OrganizeOperationStatus.Planning);
        await RunOrganizeJobAsync();
        return await OperationAsync(result.Operation.Id);
    }

    protected async Task<OrganizeOperation> OperationAsync(Guid operationId)
    {
        await using WaveMpDbContext db = CreateDbContext();
        return await db.OrganizeOperations.AsNoTracking().SingleAsync(operation => operation.Id == operationId);
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
        services.AddSingleton(Jobs);
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Replaces services of a fixture; runs last.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
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
