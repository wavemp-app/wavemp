using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// Result of changing the settings of the organization: the saved settings or the errors why nothing was saved.
/// </summary>
public sealed record OrganizingSettingsResult(
    OrganizingSettings? Settings,
    IReadOnlyList<OrganizingSettingsValidationError> Errors)
{
    [MemberNotNullWhen(true, nameof(Settings))]
    public bool IsSuccess => Settings is not null;

    public static OrganizingSettingsResult Success(OrganizingSettings settings)
    {
        return new OrganizingSettingsResult(settings, []);
    }

    public static OrganizingSettingsResult Failure(IReadOnlyList<OrganizingSettingsValidationError> errors)
    {
        return new OrganizingSettingsResult(null, errors);
    }
}
