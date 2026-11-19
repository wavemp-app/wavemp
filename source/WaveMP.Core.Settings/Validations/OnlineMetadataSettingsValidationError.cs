namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// A rejected value of the settings of the online search, with the limits that apply
/// (<see cref="SettingsErrorReason.OutOfRange"/>: both).
/// </summary>
public sealed record OnlineMetadataSettingsValidationError(
    OnlineMetadataSettingsField Field,
    SettingsErrorReason Reason,
    int? Minimum = null,
    int? Maximum = null);
