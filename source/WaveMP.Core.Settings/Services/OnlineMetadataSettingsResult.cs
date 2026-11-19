using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// Result of changing the settings of the online search: the saved settings or the errors why nothing was saved.
/// </summary>
public sealed record OnlineMetadataSettingsResult(
    OnlineMetadataSettings? Settings,
    IReadOnlyList<OnlineMetadataSettingsValidationError> Errors)
{
    [MemberNotNullWhen(true, nameof(Settings))]
    public bool IsSuccess => Settings is not null;

    public static OnlineMetadataSettingsResult Success(OnlineMetadataSettings settings)
    {
        return new OnlineMetadataSettingsResult(settings, []);
    }

    public static OnlineMetadataSettingsResult Failure(IReadOnlyList<OnlineMetadataSettingsValidationError> errors)
    {
        return new OnlineMetadataSettingsResult(null, errors);
    }
}
