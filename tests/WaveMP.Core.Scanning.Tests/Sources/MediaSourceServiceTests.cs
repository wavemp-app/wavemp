using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Options;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Sources;
using WaveMP.Core.Scanning.Watchers;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Scanning.Tests.Sources;

[TestFixture]
public class MediaSourceServiceTests
{
    private static readonly JsonSerializerOptions _payloadOptions = new(JsonSerializerDefaults.Web);

    private TemporaryDirectory _root = null!;
    private string _media = null!;
    private List<MediaSource> _stored = null!;
    private IMediaSourceRepository _repository = null!;
    private IMediaSourceCatalog _catalog = null!;
    private IJobService _jobs = null!;
    private JobRunTracker _tracker = null!;
    private ServiceProvider _services = null!;
    private MediaFolderWatcher _watcher = null!;
    private MediaSourceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _root = new TemporaryDirectory();
        _media = _root.CreateDirectory("media");
        _root.CreateDirectory("videos");
        _root.CreateDirectory("media/inner");
        _stored =
        [
            new MediaSource
            {
                Id = MediaSource.DefaultId,
                Name = "Media",
                NormalizedName = "MEDIA",
                Path = null,
                IsActive = true,
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch,
            },
        ];
        _repository = Substitute.For<IMediaSourceRepository>();
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<MediaSource>)[.. _stored]);
        _repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.Find(source => source.Id == call.Arg<Guid>()));
        _repository.NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.Exists(source => source.NormalizedName == call.ArgAt<string>(0)
                && source.Id != call.ArgAt<Guid?>(1)));
        _repository.AddAsync(Arg.Any<MediaSource>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _stored.Add(call.Arg<MediaSource>());
                return Task.CompletedTask;
            });
        _repository.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.RemoveAll(source => source.Id == call.Arg<Guid>()) > 0);
        _repository.CountAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MediaSourceCounts> { [MediaSource.DefaultId] = new(3, 5) });
        _catalog = Substitute.For<IMediaSourceCatalog>();
        _catalog.Sources.Returns(_ => [.. _stored.Select(source => new MediaSourceInfo(
            source.Id,
            source.Name,
            source.Path ?? _media,
            source.IsActive,
            source.Path is null))]);
        _jobs = Substitute.For<IJobService>();
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Success(new JobRun
            {
                Id = Guid.NewGuid(),
                JobId = JobIds.LibraryScan,
                Trigger = JobRunTrigger.Manual,
                Status = JobRunStatus.Queued,
                QueuedAt = DateTimeOffset.UtcNow,
            }));
        _tracker = new JobRunTracker();
        ISettingsSnapshot<LibrarySettings> settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        settings.Current.Returns(new LibrarySettings());
        _services = new ServiceCollection().BuildServiceProvider();
        _watcher = new MediaFolderWatcher(
            _catalog,
            Microsoft.Extensions.Options.Options.Create(new ScanOptions()),
            settings,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _tracker,
            TimeProvider.System,
            NullLogger<MediaFolderWatcher>.Instance);
        _service = new MediaSourceService(
            _repository,
            _catalog,
            _jobs,
            _tracker,
            _watcher,
            new LibraryWriteLock(),
            Microsoft.Extensions.Options.Options.Create(new MediaLibraryOptions { RootPath = _media }),
            TimeProvider.System,
            NullLogger<MediaSourceService>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        _watcher.Dispose();
        await _services.DisposeAsync();
        _root.Dispose();
    }

    [Test]
    public async Task ListAsync_FirstSource_FollowsMediaPathWithItsCounts()
    {
        IReadOnlyList<MediaSourceOverview> sources = await _service.ListAsync();

        MediaSourceOverview first = sources.Should().ContainSingle().Subject;
        first.Path.Should().Be(_media);
        first.FollowsMediaPath.Should().BeTrue();
        first.IsAvailable.Should().BeTrue();
        (first.TrackCount, first.FileCount).Should().Be((3, 5));
    }

    [Test]
    public async Task ListAsync_FolderMissingAfterTheWatcherStarted_IsNeitherAvailableNorWatched()
    {
        await _watcher.StartAsync(CancellationToken.None);
        (await _service.ListAsync()).Single().IsWatched.Should().BeTrue();

        Directory.Delete(_media, recursive: true);
        MediaSourceOverview source = (await _service.ListAsync()).Single();

        source.IsAvailable.Should().BeFalse();
        source.IsWatched.Should().BeFalse();
    }

    [Test]
    public async Task CreateAsync_ValidSource_SavesItAndScansIt()
    {
        string videos = _root.Combine("videos");

        MediaSourceResult result = await _service.CreateAsync(new CreateMediaSourceCommand(" Videos ", videos, null));

        result.IsSuccess.Should().BeTrue();
        result.Source!.Name.Should().Be("Videos");
        result.Source.Path.Should().Be(videos);
        result.Source.IsActive.Should().BeTrue();
        result.Source.FollowsMediaPath.Should().BeFalse();
        _stored.Should().Contain(source => source.NormalizedName == "VIDEOS");
        await _catalog.Received().RefreshAsync(Arg.Any<CancellationToken>());
        JobRunRequest request = StartedScan();
        request.Trigger.Should().Be(JobRunTrigger.Manual);
        JsonSerializer.Deserialize<SourceFolder[]>(request.Payload!, _payloadOptions)
            .Should().Equal(new SourceFolder(result.Source.Id, string.Empty));
    }

    [Test]
    public async Task CreateAsync_Inactive_IsNotScanned()
    {
        MediaSourceResult result = await _service.CreateAsync(
            new CreateMediaSourceCommand("Videos", _root.Combine("videos"), false));

        result.Source!.IsActive.Should().BeFalse();
        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    [Test]
    public async Task CreateAsync_ScanCannotStart_StillSavesTheSource()
    {
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Failure(JobRunStartError.AlreadyActive));

        MediaSourceResult result = await _service.CreateAsync(
            new CreateMediaSourceCommand("Videos", _root.Combine("videos"), null));

        result.IsSuccess.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_EmptyName_IsInvalid(string name)
    {
        MediaSourceResult result = await _service.CreateAsync(
            new CreateMediaSourceCommand(name, _root.Combine("videos"), null));

        result.Error.Should().Be(MediaSourceError.InvalidName);
    }

    [Test]
    public async Task CreateAsync_NameTakenWithAnotherCase_IsRejected()
    {
        MediaSourceResult result = await _service.CreateAsync(
            new CreateMediaSourceCommand("media", _root.Combine("videos"), null));

        result.Error.Should().Be(MediaSourceError.NameTaken);
    }

    [TestCase("")]
    [TestCase("videos")]
    [TestCase("./videos")]
    public async Task CreateAsync_RelativePath_IsInvalid(string path)
    {
        MediaSourceResult result = await _service.CreateAsync(new CreateMediaSourceCommand("Videos", path, null));

        result.Error.Should().Be(MediaSourceError.InvalidPath);
    }

    [Test]
    public async Task CreateAsync_MissingFolder_IsRejected()
    {
        MediaSourceResult result = await _service.CreateAsync(
            new CreateMediaSourceCommand("Videos", _root.Combine("missing"), null));

        result.Error.Should().Be(MediaSourceError.PathNotFound);
        _stored.Should().ContainSingle();
    }

    [TestCase("media")]
    [TestCase("media/inner")]
    [TestCase("")]
    public async Task CreateAsync_FolderOverlappingAnotherSource_IsRejected(string relative)
    {
        string path = relative.Length == 0 ? _root.Path : _root.Combine(relative);

        MediaSourceResult result = await _service.CreateAsync(new CreateMediaSourceCommand("Videos", path, null));

        result.Error.Should().Be(MediaSourceError.PathOverlaps);
    }

    [Test]
    public async Task UpdateAsync_Deactivate_KeepsTheSourceWithoutAScan()
    {
        MediaSourceResult result = await _service.UpdateAsync(
            MediaSource.DefaultId,
            new UpdateMediaSourceCommand(null, null, false));

        result.Source!.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(
            Arg.Is<MediaSource>(source => !source.IsActive),
            Arg.Any<CancellationToken>());
        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    [Test]
    public async Task UpdateAsync_Reactivate_ScansTheSource()
    {
        _stored[0].IsActive = false;

        await _service.UpdateAsync(MediaSource.DefaultId, new UpdateMediaSourceCommand(null, null, true));

        StartedScan().Payload.Should().Contain(MediaSource.DefaultId.ToString());
    }

    [Test]
    public async Task UpdateAsync_NewFolderOfItsOwn_StopsFollowingMediaPathAndScans()
    {
        string videos = _root.Combine("videos");

        MediaSourceResult result = await _service.UpdateAsync(
            MediaSource.DefaultId,
            new UpdateMediaSourceCommand("Music", videos, null));

        result.Source!.Name.Should().Be("Music");
        result.Source.Path.Should().Be(videos);
        result.Source.FollowsMediaPath.Should().BeFalse();
        StartedScan();
    }

    [Test]
    public async Task UpdateAsync_NewFolderWhileAScanRuns_IsRejected()
    {
        _tracker.MarkRunning(JobIds.LibraryScan, Guid.NewGuid(), "fire");

        MediaSourceResult result = await _service.UpdateAsync(
            MediaSource.DefaultId,
            new UpdateMediaSourceCommand(null, _root.Combine("videos"), null));

        result.Error.Should().Be(MediaSourceError.ScanRunning);
        _stored[0].Path.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_RenameWhileAScanRuns_IsAllowed()
    {
        _tracker.MarkRunning(JobIds.LibraryScan, Guid.NewGuid(), "fire");

        MediaSourceResult result = await _service.UpdateAsync(
            MediaSource.DefaultId,
            new UpdateMediaSourceCommand("Music", null, null));

        result.Source!.Name.Should().Be("Music");
    }

    [Test]
    public async Task UpdateAsync_UnknownSource_IsNotFound()
    {
        MediaSourceResult result = await _service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMediaSourceCommand("Music", null, null));

        result.Error.Should().Be(MediaSourceError.NotFound);
    }

    [Test]
    public async Task DeleteAsync_Source_RemovesItAndRefreshesTheCatalog()
    {
        MediaSourceError? error = await _service.DeleteAsync(MediaSource.DefaultId);

        error.Should().BeNull();
        _stored.Should().BeEmpty();
        await _catalog.Received().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_WhileAScanRuns_IsRejected()
    {
        _tracker.MarkRunning(JobIds.LibraryScan, Guid.NewGuid(), "fire");

        MediaSourceError? error = await _service.DeleteAsync(MediaSource.DefaultId);

        error.Should().Be(MediaSourceError.ScanRunning);
        _stored.Should().ContainSingle();
    }

    [Test]
    public async Task DeleteAsync_UnknownSource_IsNotFound()
    {
        (await _service.DeleteAsync(Guid.NewGuid())).Should().Be(MediaSourceError.NotFound);
    }

    private JobRunRequest StartedScan()
    {
        return (JobRunRequest)_jobs.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IJobService.StartRunAsync))
            .GetArguments()[0]!;
    }
}
