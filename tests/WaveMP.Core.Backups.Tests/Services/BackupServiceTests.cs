using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Backups.Tests.Services;

[TestFixture]
public class BackupServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private IBackupStore _store = null!;
    private IDatabaseExporter _exporter = null!;
    private ISettingsSnapshot<BackupSettings> _settings = null!;
    private BackupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = Substitute.For<IBackupStore>();
        _store.Prepare(Arg.Any<DateTimeOffset>(), Arg.Any<BackupTrigger>())
            .Returns(call => BackupFileNames.CreateId(call.Arg<DateTimeOffset>(), call.Arg<BackupTrigger>()));
        _store.PartialPathOf(Arg.Any<string>()).Returns(
            call => "/data/backups/" + call.Arg<string>() + ".dump.partial");
        _store.Complete(Arg.Any<string>()).Returns(call => FileOf(call.Arg<string>()));
        _store.ApplyRetention(Arg.Any<int>()).Returns([]);
        _exporter = Substitute.For<IDatabaseExporter>();
        _settings = Substitute.For<ISettingsSnapshot<BackupSettings>>();
        _settings.Current.Returns(new BackupSettings { RetentionCount = 5 });
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now);
        _service = new BackupService(
            _store,
            _exporter,
            _settings,
            timeProvider,
            NullLogger<BackupService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task CreateAsync_Scheduled_ExportsCompletesAndKeepsTheConfiguredNumber()
    {
        BackupFile old = FileOf(BackupFileNames.CreateId(_now.AddDays(-9), BackupTrigger.Schedule));
        _store.ApplyRetention(5).Returns([old]);

        BackupResult result = await _service.CreateAsync(BackupTrigger.Schedule);

        string id = "wavemp-20261116T030000Z-schedule";
        result.Created.Should().Be(FileOf(id));
        result.Deleted.Should().Equal(old);
        Received.InOrder(() =>
        {
            _store.Prepare(_now, BackupTrigger.Schedule);
            _exporter.ExportAsync("/data/backups/" + id + ".dump.partial", Arg.Any<CancellationToken>());
            _store.Complete(id);
            _store.ApplyRetention(5);
        });
    }

    [Test]
    public async Task CreateAsync_BeforeMigrations_KeepsAllOtherExports()
    {
        BackupResult result = await _service.CreateAsync(BackupTrigger.Migration);

        result.Created.Trigger.Should().Be(BackupTrigger.Migration);
        result.Deleted.Should().BeEmpty();
        _store.DidNotReceive().ApplyRetention(Arg.Any<int>());
    }

    [Test]
    public async Task CreateAsync_WhenTheExportFails_DiscardsTheFileAndDeletesNothing()
    {
        _exporter.ExportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFailedException(BackupError.ExportFailed, "pg_dump: error"));

        Func<Task> create = () => _service.CreateAsync(BackupTrigger.Manual);

        (await create.Should().ThrowAsync<BackupFailedException>()).Which.Error.Should().Be(BackupError.ExportFailed);
        _store.Received(1).Discard("wavemp-20261116T030000Z-manual");
        _store.DidNotReceive().Complete(Arg.Any<string>());
        _store.DidNotReceive().ApplyRetention(Arg.Any<int>());
    }

    [Test]
    public async Task CreateAsync_WhenCanceled_DiscardsTheFile()
    {
        _exporter.ExportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        Func<Task> create = () => _service.CreateAsync(BackupTrigger.Schedule);

        await create.Should().ThrowAsync<OperationCanceledException>();
        _store.Received(1).Discard("wavemp-20261116T030000Z-schedule");
    }

    [Test]
    public async Task CreateAsync_WhenTheFolderIsUnwritable_ExportsNothing()
    {
        _store.Prepare(Arg.Any<DateTimeOffset>(), Arg.Any<BackupTrigger>())
            .Throws(new BackupFailedException(BackupError.FolderUnwritable, "read-only"));

        Func<Task> create = () => _service.CreateAsync(BackupTrigger.Schedule);

        (await create.Should().ThrowAsync<BackupFailedException>())
            .Which.Error.Should().Be(BackupError.FolderUnwritable);
        await _exporter.DidNotReceive().ExportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void List_WithPaging_ReturnsThePageAndTheTotal()
    {
        BackupFile[] files =
        [
            FileOf(BackupFileNames.CreateId(_now, BackupTrigger.Manual)),
            FileOf(BackupFileNames.CreateId(_now.AddDays(-1), BackupTrigger.Schedule)),
            FileOf(BackupFileNames.CreateId(_now.AddDays(-2), BackupTrigger.Schedule)),
        ];
        _store.List().Returns(files);

        Page<BackupFile> page = _service.List(1, 1);

        page.Items.Should().Equal(files[1]);
        page.Total.Should().Be(3);
    }

    [Test]
    public void Delete_Always_ReturnsWhetherTheStoreDeletedIt()
    {
        _store.Delete("wavemp-20261116T030000Z-manual").Returns(true);

        _service.Delete("wavemp-20261116T030000Z-manual").Should().BeTrue();
        _service.Delete("other").Should().BeFalse();
    }

    private static BackupFile FileOf(string id)
    {
        BackupFileNames.TryParseId(id, out DateTimeOffset createdAt, out BackupTrigger trigger, out _);
        return new BackupFile(id, id + ".dump", createdAt, 1024, trigger);
    }
}
