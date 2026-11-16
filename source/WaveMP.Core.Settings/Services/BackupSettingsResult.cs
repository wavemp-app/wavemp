using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// Result of changing the backup settings: the saved settings or the errors why nothing was saved.
/// </summary>
public sealed record BackupSettingsResult(
    BackupSettings? Settings,
    IReadOnlyList<BackupSettingsValidationError> Errors)
{
    [MemberNotNullWhen(true, nameof(Settings))]
    public bool IsSuccess => Settings is not null;

    public static BackupSettingsResult Success(BackupSettings settings)
    {
        return new BackupSettingsResult(settings, []);
    }

    public static BackupSettingsResult Failure(IReadOnlyList<BackupSettingsValidationError> errors)
    {
        return new BackupSettingsResult(null, errors);
    }
}
