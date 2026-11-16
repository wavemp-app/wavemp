using System.Text.Json;
using AwesomeAssertions;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class BackupMappingExtensionsTests
{
    [Test]
    public void ToDto_File_MapsIdNameTimeSizeAndTrigger()
    {
        DateTimeOffset createdAt = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);
        BackupFile file = new(
            "wavemp-20261116T030000Z-schedule",
            "wavemp-20261116T030000Z-schedule.dump",
            createdAt,
            4096,
            BackupTrigger.Schedule);

        file.ToDto().Should().BeEquivalentTo(new BackupDto
        {
            Id = "wavemp-20261116T030000Z-schedule",
            FileName = "wavemp-20261116T030000Z-schedule.dump",
            CreatedAt = createdAt,
            SizeBytes = 4096,
            Trigger = BackupTriggerDto.Schedule,
        });
    }

    [Test]
    public void ToDto_EveryTrigger_HasAName()
    {
        Enum.GetValues<BackupTrigger>().Select(trigger => JsonSerializer.Serialize(trigger.ToDto()))
            .Should().Equal("\"Schedule\"", "\"Manual\"", "\"Migration\"");
    }

    [Test]
    public void ToDtoAndToCommand_Settings_MapTheTimeAsHoursAndMinutes()
    {
        new BackupSettings { ScheduleTime = new TimeOnly(4, 5), RetentionCount = 9 }.ToDto()
            .Should().BeEquivalentTo(new BackupSettingsDto { ScheduleTime = "04:05", RetentionCount = 9 });
        new UpdateBackupSettingsDto { ScheduleTime = "23:59", RetentionCount = 3 }.ToCommand()
            .Should().Be(new UpdateBackupSettingsCommand(new TimeOnly(23, 59), 3));
        new UpdateBackupSettingsDto().ToCommand().Should().Be(new UpdateBackupSettingsCommand());
    }

    [Test]
    public void ToJsonName_EveryField_IsTheNameInTheRequest()
    {
        Enum.GetValues<BackupSettingsField>().Select(field => field.ToJsonName())
            .Should().Equal("scheduleTime", "retentionCount");
    }
}
