namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Changes the settings of the organization of the media files. Every <c>null</c> value keeps the current value.
/// </summary>
public sealed record UpdateOrganizingSettingsCommand(string? LayoutTemplate = null);
