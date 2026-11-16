using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Service.Tests.IntegrationTests.Fakes;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The database backups: the daily job, manual exports, the list, deleting exports and the backup settings. pg_dump is
/// replaced by <see cref="FakeDatabaseExporter"/>; the E2E tests export with the real pg_dump of the image.
/// </summary>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class BackupsApiTests
{
    private const string JobId = "database-backup";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    private string BackupFolder => Path.Combine(_factory.DataPath, "backups");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_backups",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        _user = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _user?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _admin.SendJsonAsync("PATCH", "/api/v1/settings/backup", """{"scheduleTime":"03:00","retentionCount":7}""");
        if (Directory.Exists(BackupFolder))
        {
            Directory.Delete(BackupFolder, recursive: true);
        }
    }

    private static IEnumerable<TestCaseData> Endpoints()
    {
        yield return new TestCaseData("GET", "/api/v1/backups").SetArgDisplayNames("ListBackups");
        yield return new TestCaseData("POST", "/api/v1/backups").SetArgDisplayNames("CreateBackup");
        yield return new TestCaseData("DELETE", "/api/v1/backups/wavemp-20261116T030000Z-manual")
            .SetArgDisplayNames("DeleteBackup");
        yield return new TestCaseData("GET", "/api/v1/settings/backup").SetArgDisplayNames("GetBackupSettings");
        yield return new TestCaseData("PATCH", "/api/v1/settings/backup").SetArgDisplayNames("UpdateBackupSettings");
        yield return new TestCaseData("GET", "/api/v1/jobs/database-backup").SetArgDisplayNames("GetJob");
    }

    [TestCaseSource(nameof(Endpoints))]
    public async Task BackupEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        HttpResponseMessage response = await _anonymous.SendJsonAsync(method, path, method == "PATCH" ? "{}" : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(Endpoints))]
    public async Task BackupEndpoints_AsUser_ReturnForbidden(string method, string path)
    {
        HttpResponseMessage response = await _user.SendJsonAsync(method, path, method == "PATCH" ? "{}" : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task GetJob_DatabaseBackup_RunsDailyAtThreeByDefault()
    {
        JobDto job = await _admin.GetJsonAsync<JobDto>("/api/v1/jobs/database-backup");

        job.Id.Should().Be(JobIdDto.DatabaseBackup);
        job.DailyAt.Should().Be("03:00");
        job.IntervalMinutes.Should().Be(1440);
        job.NextRunAt.Should().NotBeNull();
        job.NextRunAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
        job.NextRunAt.Value.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddDays(1));
        LocalTimeOfDay(job.NextRunAt.Value).Should().Be(new TimeSpan(3, 0, 0), "the schedule uses the local time");
        job.TimeZone.Should().Be(TimeZoneInfo.Local.ToIanaId(), "the schedule names the time zone of the server");
        job.TimeZone.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetJob_UnknownJob_ReturnsNotFound()
    {
        HttpResponseMessage response = await _admin.GetAsync("/api/v1/jobs/unknown");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "job.not_found");
    }

    [Test]
    public async Task CreateBackup_AsAdmin_ExportsTheDatabaseIntoTheBackupFolder()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);

        JobRunDto run = await CreateBackupAsync();
        PageDto<BackupDto> backups = await _admin.GetJsonAsync<PageDto<BackupDto>>("/api/v1/backups");

        run.Status.Should().Be(JobRunStatusDto.Succeeded);
        run.Trigger.Should().Be(JobTriggerDto.Manual);
        BackupDto backup = backups.Items.Should().ContainSingle().Subject;
        backups.Total.Should().Be(1);
        backup.Trigger.Should().Be(BackupTriggerDto.Manual);
        backup.SizeBytes.Should().Be(FakeDatabaseExporter.Content.Length);
        backup.CreatedAt.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        backup.FileName.Should().Be(backup.Id + ".dump");
        backup.Id.Should().MatchRegex("^wavemp-[0-9]{8}T[0-9]{6}Z-manual(-[0-9]+)?$");
        File.ReadAllBytes(Path.Combine(BackupFolder, backup.FileName)).Should().Equal(FakeDatabaseExporter.Content);
        Directory.EnumerateFiles(BackupFolder, "*.partial").Should().BeEmpty();
    }

    [Test]
    public async Task CreateBackup_Always_RecordsTheRunInTheHistoryOfTheJob()
    {
        JobRunDto run = await CreateBackupAsync();

        JobDto job = await _admin.GetJsonAsync<JobDto>("/api/v1/jobs/database-backup");

        job.LastRun!.Id.Should().Be(run.Id);
        job.LastRun.Status.Should().Be(JobRunStatusDto.Succeeded);
        job.State.Should().Be(JobStateDto.Idle);
    }

    [Test]
    public async Task CreateBackup_Always_KeepsOnlyTheConfiguredNumberOfExports()
    {
        await _admin.SendJsonAsync("PATCH", "/api/v1/settings/backup", """{"retentionCount":2}""");

        for (int index = 0; index < 3; index++)
        {
            (await CreateBackupAsync()).Status.Should().Be(JobRunStatusDto.Succeeded);
        }

        PageDto<BackupDto> backups = await _admin.GetJsonAsync<PageDto<BackupDto>>("/api/v1/backups");
        backups.Total.Should().Be(2);
        Directory.EnumerateFiles(BackupFolder, "*.dump").Should().HaveCount(2);
    }

    [Test]
    public async Task ListBackups_Always_ListsNewestFirstAndIgnoresOtherFiles()
    {
        Directory.CreateDirectory(BackupFolder);
        await File.WriteAllTextAsync(Path.Combine(BackupFolder, "wavemp-20261101T030000Z-schedule.dump"), "a");
        await File.WriteAllTextAsync(Path.Combine(BackupFolder, "wavemp-20261102T030000Z-migration.dump"), "bb");
        await File.WriteAllTextAsync(Path.Combine(BackupFolder, "my-own-backup.dump"), "other");

        PageDto<BackupDto> page = await _admin.GetJsonAsync<PageDto<BackupDto>>("/api/v1/backups?limit=1");

        page.Total.Should().Be(2);
        BackupDto newest = page.Items.Should().ContainSingle().Subject;
        newest.Id.Should().Be("wavemp-20261102T030000Z-migration");
        newest.Trigger.Should().Be(BackupTriggerDto.Migration);
        newest.SizeBytes.Should().Be(2);
        newest.CreatedAt.Should().Be(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task DeleteBackup_WithExport_DeletesItsFile()
    {
        Directory.CreateDirectory(BackupFolder);
        string path = Path.Combine(BackupFolder, "wavemp-20261101T030000Z-schedule.dump");
        await File.WriteAllTextAsync(path, "a");

        HttpResponseMessage first = await _admin.DeleteAsync("/api/v1/backups/wavemp-20261101T030000Z-schedule");
        HttpResponseMessage second = await _admin.DeleteAsync("/api/v1/backups/wavemp-20261101T030000Z-schedule");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(path).Should().BeFalse();
        await second.ShouldBeProblemAsync(HttpStatusCode.NotFound, "backup.not_found");
    }

    [TestCase("my-own-backup")]
    [TestCase("..%2Fsecret")]
    public async Task DeleteBackup_WithOtherName_ReturnsNotFoundAndKeepsTheFile(string id)
    {
        Directory.CreateDirectory(BackupFolder);
        string path = Path.Combine(BackupFolder, "my-own-backup.dump");
        await File.WriteAllTextAsync(path, "other");

        HttpResponseMessage response = await _admin.DeleteAsync($"/api/v1/backups/{id}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "backup.not_found");
        File.Exists(path).Should().BeTrue();
    }

    [Test]
    public async Task UpdateBackupSettings_WithValidValues_SavesThemAndPlansTheBackupAgain()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/backup",
            """{"scheduleTime":"22:15","retentionCount":30}""");
        BackupSettingsDto saved = await _admin.GetJsonAsync<BackupSettingsDto>("/api/v1/settings/backup");
        JobDto job = await _admin.GetJsonAsync<JobDto>("/api/v1/jobs/database-backup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.ScheduleTime.Should().Be("22:15");
        saved.RetentionCount.Should().Be(30);
        job.DailyAt.Should().Be("22:15");
        LocalTimeOfDay(job.NextRunAt!.Value).Should().Be(new TimeSpan(22, 15, 0));
    }

    [Test]
    public async Task UpdateBackupSettings_WithInvalidValues_ReturnsTheErrorsByField()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/backup",
            """{"scheduleTime":"24:00","retentionCount":0}""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle(error => error.Field == "scheduleTime")
            .Which.Code.Should().Be(ValidationErrorCodeDto.InvalidFormat);
        ValidationErrorDto retention = errors.Should().ContainSingle(error => error.Field == "retentionCount").Subject;
        retention.Code.Should().Be(ValidationErrorCodeDto.OutOfRange);
        retention.Min.Should().Be(1);
        retention.Max.Should().Be(100);
        (await _admin.GetJsonAsync<BackupSettingsDto>("/api/v1/settings/backup")).RetentionCount.Should().Be(7);
    }

    private static TimeSpan LocalTimeOfDay(DateTimeOffset time)
    {
        return TimeZoneInfo.ConvertTime(time, TimeZoneInfo.Local).TimeOfDay;
    }

    private async Task<JobRunDto> CreateBackupAsync()
    {
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/backups", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        JobRunDto queued = (await response.Content.ReadFromJsonAsync<JobRunDto>(HttpClientExtensions.JsonOptions))!;
        queued.JobId.Should().Be(JobIdDto.DatabaseBackup);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/jobs/{JobId}/runs/{queued.Id}");
        JobRunDto finished = await _admin.WaitForJobRunAsync(JobId, queued.Id);
        // The job is free for the next run once the history of this one is written.
        await _admin.WaitForJobsIdleAsync();
        return finished;
    }
}
