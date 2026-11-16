namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Changes the backup settings. Every <c>null</c> value keeps the current value.
/// </summary>
public sealed record UpdateBackupSettingsCommand(TimeOnly? ScheduleTime = null, int? RetentionCount = null);
