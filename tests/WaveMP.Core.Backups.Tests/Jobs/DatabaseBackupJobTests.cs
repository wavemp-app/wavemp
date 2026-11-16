using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Jobs;
using WaveMP.Core.Backups.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Backups.Tests.Jobs;

[TestFixture]
public class DatabaseBackupJobTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private IBackupService _backups = null!;
    private DatabaseBackupJob _job = null!;

    [SetUp]
    public void SetUp()
    {
        _backups = Substitute.For<IBackupService>();
        _job = new DatabaseBackupJob(_backups);
    }

    [Test]
    public async Task Execute_StartedByAnAdmin_CreatesAManualExportAndRecordsTheResult()
    {
        BackupFile created = new("id", "id.dump", _now, 2048, BackupTrigger.Manual);
        BackupFile old = new("old", "old.dump", _now.AddDays(-8), 1024, BackupTrigger.Schedule);
        _backups.CreateAsync(BackupTrigger.Manual, Arg.Any<CancellationToken>())
            .Returns(new BackupResult(created, [old]));
        IJobExecutionContext context = ContextWith(nameof(JobRunTrigger.Manual));

        await _job.Execute(context, CancellationToken.None);

        context.Result.Should().Be(new DatabaseBackupJobResult("id.dump", 2048, 1));
    }

    [Test]
    public async Task Execute_BySchedule_CreatesAScheduledExport()
    {
        _backups.CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>())
            .Returns(new BackupResult(new BackupFile("id", "id.dump", _now, 1, BackupTrigger.Schedule), []));

        await _job.Execute(ContextWith(null), CancellationToken.None);

        await _backups.Received(1).CreateAsync(BackupTrigger.Schedule, Arg.Any<CancellationToken>());
    }

    [TestCase(BackupError.FolderUnwritable, JobErrorCode.BackupFolderUnwritable)]
    [TestCase(BackupError.PgDumpUnavailable, JobErrorCode.PgDumpUnavailable)]
    [TestCase(BackupError.ExportFailed, JobErrorCode.BackupFailed)]
    public async Task Execute_WhenTheExportFails_FailsWithTheCodeOfTheError(BackupError error, JobErrorCode expected)
    {
        _backups.CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFailedException(error, "failed"));

        Func<Task> execute = async () => await _job.Execute(ContextWith(null), CancellationToken.None);

        (await execute.Should().ThrowAsync<JobFailedException>()).Which.Code.Should().Be(expected);
    }

    [TestCase(nameof(JobRunTrigger.Manual), BackupTrigger.Manual)]
    [TestCase(nameof(JobRunTrigger.Schedule), BackupTrigger.Schedule)]
    [TestCase(null, BackupTrigger.Schedule)]
    public void TriggerOf_Always_TellsManualRunsFromTheSchedule(string? trigger, BackupTrigger expected)
    {
        JobDataMap data = [];
        if (trigger is not null)
        {
            data[JobDataKeys.Trigger] = trigger;
        }

        DatabaseBackupJob.TriggerOf(data).Should().Be(expected);
    }

    private static IJobExecutionContext ContextWith(string? trigger)
    {
        JobDataMap data = [];
        if (trigger is not null)
        {
            data[JobDataKeys.Trigger] = trigger;
        }

        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(data);
        return context;
    }
}
