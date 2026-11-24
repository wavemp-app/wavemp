namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the settings of the organization of the media files. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateOrganizingSettingsDto
{
    /// <summary>
    /// The layout template (see <see cref="OrganizingSettingsDto.LayoutTemplate"/>), at most 512 characters;
    /// surrounding white space is removed. PreviewLayoutTemplate returns the detailed problems of an invalid
    /// template.
    /// </summary>
    public string? LayoutTemplate { get; init; }
}
