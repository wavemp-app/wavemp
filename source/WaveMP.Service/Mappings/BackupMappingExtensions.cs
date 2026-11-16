using System.Globalization;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class BackupMappingExtensions
{
    /// <summary>
    /// Format of times of day in the API: hours and minutes, 24 hours.
    /// </summary>
    public const string TimeFormat = "HH:mm";

    public static BackupDto ToDto(this BackupFile file)
    {
        return new BackupDto
        {
            Id = file.Id,
            FileName = file.FileName,
            CreatedAt = file.CreatedAt,
            SizeBytes = file.SizeBytes,
            Trigger = file.Trigger.ToDto(),
        };
    }

    public static BackupTriggerDto ToDto(this BackupTrigger trigger)
    {
        return trigger switch
        {
            BackupTrigger.Schedule => BackupTriggerDto.Schedule,
            BackupTrigger.Manual => BackupTriggerDto.Manual,
            BackupTrigger.Migration => BackupTriggerDto.Migration,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unknown backup trigger."),
        };
    }

    public static BackupSettingsDto ToDto(this BackupSettings settings)
    {
        return new BackupSettingsDto
        {
            ScheduleTime = settings.ScheduleTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            RetentionCount = settings.RetentionCount,
        };
    }

    /// <summary>
    /// The command for the changes of a request. The time has already been checked against its pattern by the
    /// validation of the request.
    /// </summary>
    public static UpdateBackupSettingsCommand ToCommand(this UpdateBackupSettingsDto request)
    {
        TimeOnly? scheduleTime = request.ScheduleTime is { } text
            ? TimeOnly.ParseExact(text, TimeFormat, CultureInfo.InvariantCulture)
            : null;
        return new UpdateBackupSettingsCommand(scheduleTime, request.RetentionCount);
    }

    /// <summary>
    /// The JSON name of the field in <see cref="UpdateBackupSettingsDto"/>, the key of its validation errors.
    /// </summary>
    public static string ToJsonName(this BackupSettingsField field)
    {
        return field switch
        {
            BackupSettingsField.ScheduleTime => "scheduleTime",
            BackupSettingsField.RetentionCount => "retentionCount",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }
}
