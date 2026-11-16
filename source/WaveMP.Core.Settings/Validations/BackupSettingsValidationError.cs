namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// A rejected value of the backup settings, with the limits that apply (<see cref="SettingsErrorReason.OutOfRange"/>:
/// both; <see cref="SettingsErrorReason.Invalid"/>: none).
/// </summary>
public sealed record BackupSettingsValidationError(
    BackupSettingsField Field,
    SettingsErrorReason Reason,
    int? Minimum = null,
    int? Maximum = null);
