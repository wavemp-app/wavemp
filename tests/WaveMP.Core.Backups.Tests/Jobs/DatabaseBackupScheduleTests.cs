using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Backups.Jobs;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Schedulers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.Backups.Tests.Jobs;

/// <summary>
/// The daily schedule of the backup and its update when the settings change.
/// </summary>
[TestFixture]
public class DatabaseBackupScheduleTests
{
    [TestCase(JobScheduleReason.Startup)]
    [TestCase(JobScheduleReason.SettingsChanged)]
    public void GetSchedule_Always_RunsDailyAtTheTimeOfTheSettingsInTheLocalTimeZone(JobScheduleReason reason)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+01", TimeSpan.FromHours(1), "Test+01", "Test+01");
        ISettingsSnapshot<BackupSettings> settings = Substitute.For<ISettingsSnapshot<BackupSettings>>();
        settings.Current.Returns(new BackupSettings { ScheduleTime = new TimeOnly(2, 45) });
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(zone);
        DatabaseBackupScheduleProvider provider = new(settings, timeProvider);

        JobSchedule? schedule = provider.GetSchedule(reason);

        provider.JobId.Should().Be(JobIds.DatabaseBackup);
        schedule.Should().Be(JobSchedule.Daily(new TimeOnly(2, 45), zone));
        schedule!.Interval.Should().Be(TimeSpan.FromDays(1));
    }

    [Test]
    public async Task OnSettingsChangedAsync_WithNewTime_PlansTheBackupAgain()
    {
        IJobScheduler scheduler = Substitute.For<IJobScheduler>();
        DatabaseBackupRescheduler rescheduler = new(scheduler);

        await rescheduler.OnSettingsChangedAsync(
            new BackupSettings(),
            new BackupSettings { ScheduleTime = new TimeOnly(4, 0) },
            CancellationToken.None);

        await scheduler.Received(1).RescheduleAsync(JobIds.DatabaseBackup, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnSettingsChangedAsync_WithOnlyANewRetention_KeepsTheSchedule()
    {
        IJobScheduler scheduler = Substitute.For<IJobScheduler>();
        DatabaseBackupRescheduler rescheduler = new(scheduler);

        await rescheduler.OnSettingsChangedAsync(
            new BackupSettings(),
            new BackupSettings { RetentionCount = 2 },
            CancellationToken.None);

        await scheduler.DidNotReceive().RescheduleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
