using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// Normalizes and checks the settings of the organization before they are saved.
/// </summary>
public static class OrganizingSettingsValidator
{
    /// <summary>
    /// The settings as they are stored: the template trimmed and in NFC.
    /// </summary>
    public static OrganizingSettings Normalize(OrganizingSettings settings)
    {
        return new OrganizingSettings { LayoutTemplate = LayoutTemplateSyntax.Normalize(settings.LayoutTemplate) };
    }

    /// <summary>
    /// All errors of normalized settings; an empty list means they are valid.
    /// </summary>
    public static IReadOnlyList<OrganizingSettingsValidationError> Validate(OrganizingSettings settings)
    {
        LayoutTemplateParseResult parsed = LayoutTemplateSyntax.Parse(settings.LayoutTemplate);
        if (parsed.IsValid)
        {
            return [];
        }

        OrganizingSettingsValidationError error = parsed.Errors switch
        {
            [{ Code: LayoutTemplateErrorCode.Required }] =>
                new(OrganizingSettingsField.LayoutTemplate, SettingsErrorReason.Empty),
            [{ Code: LayoutTemplateErrorCode.TooLong }] => new(
                OrganizingSettingsField.LayoutTemplate,
                SettingsErrorReason.TooLong,
                Maximum: LayoutTemplateSyntax.MaxLength),
            _ => new(OrganizingSettingsField.LayoutTemplate, SettingsErrorReason.Invalid, TemplateErrors: parsed.Errors),
        };
        return [error];
    }
}
