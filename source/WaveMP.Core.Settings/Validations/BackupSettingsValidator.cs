using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// Checks the backup settings before they are saved.
/// </summary>
public static class BackupSettingsValidator
{
    public const int MinRetentionCount = 1;
    public const int MaxRetentionCount = 100;

    /// <summary>
    /// All errors of the settings; an empty list means they are valid.
    /// </summary>
    public static IReadOnlyList<BackupSettingsValidationError> Validate(BackupSettings settings)
    {
        List<BackupSettingsValidationError> errors = [];

        // The schedule runs at a whole minute; the stored JSON may hold seconds.
        if (settings.ScheduleTime != new TimeOnly(settings.ScheduleTime.Hour, settings.ScheduleTime.Minute))
        {
            errors.Add(new(BackupSettingsField.ScheduleTime, SettingsErrorReason.Invalid));
        }

        if (settings.RetentionCount is < MinRetentionCount or > MaxRetentionCount)
        {
            errors.Add(new(
                BackupSettingsField.RetentionCount,
                SettingsErrorReason.OutOfRange,
                MinRetentionCount,
                MaxRetentionCount));
        }

        return errors;
    }
}
