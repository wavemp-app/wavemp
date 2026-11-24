using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// A rejected value of the settings of the organization, with the limit that applies
/// (<see cref="SettingsErrorReason.TooLong"/>: the maximum) and, for <see cref="SettingsErrorReason.Invalid"/>, the
/// problems of the layout template.
/// </summary>
public sealed record OrganizingSettingsValidationError(
    OrganizingSettingsField Field,
    SettingsErrorReason Reason,
    int? Maximum = null,
    IReadOnlyList<LayoutTemplateError>? TemplateErrors = null);
