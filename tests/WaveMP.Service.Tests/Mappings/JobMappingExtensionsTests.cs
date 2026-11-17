using System.Text.Json;
using AwesomeAssertions;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class JobMappingExtensionsTests
{
    [TestCase(JobIds.LibraryScan, JobIdDto.LibraryScan, "library-scan")]
    [TestCase(JobIds.CoverCache, JobIdDto.CoverCache, "cover-cache")]
    [TestCase(JobIds.DatabaseBackup, JobIdDto.DatabaseBackup, "database-backup")]
    [TestCase(JobIds.LyricsDownload, JobIdDto.LyricsDownload, "lyrics-download")]
    public void ToJobIdDto_EveryJob_HasItsIdInTheApi(string jobId, JobIdDto expected, string json)
    {
        jobId.ToJobIdDto().Should().Be(expected);
        JsonSerializer.Serialize(expected).Should().Be($"\"{json}\"");
    }

    [Test]
    public void ToDto_EveryTrigger_MapsBothWays()
    {
        foreach (JobRunTrigger trigger in Enum.GetValues<JobRunTrigger>())
        {
            trigger.ToDto().ToEntity().Should().Be(trigger);
        }

        JobRunTrigger.LibraryScan.ToDto().Should().Be(JobTriggerDto.LibraryScan);
    }

    [Test]
    public void ToDto_EveryErrorCode_HasAStableCode()
    {
        Enum.GetValues<JobErrorCode>().Select(code => code.ToDto()).Should().OnlyHaveUniqueItems();
        JsonSerializer.Serialize(JobErrorCode.FfmpegUnavailable.ToDto()).Should().Be("\"cache.ffmpeg_unavailable\"");
        JsonSerializer.Serialize(JobErrorCode.CacheFolderUnwritable.ToDto())
            .Should().Be("\"cache.folder_unwritable\"");
        JsonSerializer.Serialize(JobErrorCode.PgDumpUnavailable.ToDto())
            .Should().Be("\"backup.pg_dump_unavailable\"");
    }

    [Test]
    public void ToDto_DailySchedule_NamesTheTimeZoneOfTheTimeOfDay()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var job = new JobOverview(
            JobIds.DatabaseBackup,
            JobActivityState.Idle,
            JobSchedule.Daily(new TimeOnly(3, 0), berlin),
            new DateTimeOffset(2026, 11, 17, 2, 0, 0, TimeSpan.Zero),
            null);

        JobDto dto = job.ToDto();

        dto.DailyAt.Should().Be("03:00");
        dto.TimeZone.Should().Be("Europe/Berlin");
        dto.IntervalMinutes.Should().Be(1440);
    }

    [Test]
    public void ToDto_DailyScheduleInUtc_NamesUtc()
    {
        var job = new JobOverview(
            JobIds.DatabaseBackup,
            JobActivityState.Idle,
            JobSchedule.Daily(new TimeOnly(3, 0), TimeZoneInfo.FindSystemTimeZoneById("UTC")),
            null,
            null);

        job.ToDto().TimeZone.Should().Be("UTC");
    }

    [Test]
    public void ToDto_ScheduleByInterval_HasNoTimeZone()
    {
        var job = new JobOverview(
            JobIds.LibraryScan,
            JobActivityState.Idle,
            new JobSchedule(TimeSpan.FromHours(6), TimeSpan.FromMinutes(1)),
            null,
            null);

        JobDto dto = job.ToDto();

        dto.DailyAt.Should().BeNull();
        dto.TimeZone.Should().BeNull();
    }

    [Test]
    public void ToDto_DailyScheduleInZoneWithoutIanaId_HasNoTimeZone()
    {
        var custom = TimeZoneInfo.CreateCustomTimeZone("WaveMP Test", TimeSpan.FromHours(3), "Test", "Test");
        var job = new JobOverview(
            JobIds.DatabaseBackup,
            JobActivityState.Idle,
            JobSchedule.Daily(new TimeOnly(3, 0), custom),
            null,
            null);

        JobDto dto = job.ToDto();

        dto.DailyAt.Should().Be("03:00");
        dto.TimeZone.Should().BeNull();
    }
}
