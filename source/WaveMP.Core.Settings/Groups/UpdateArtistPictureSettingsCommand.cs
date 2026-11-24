namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Changes the settings of the artist pictures. Every <c>null</c> value keeps the current value; an empty
/// <paramref name="FanartTvApiKey"/> removes the stored key.
/// </summary>
public sealed record UpdateArtistPictureSettingsCommand(
    bool? OnlineLookupEnabled = null,
    string? FanartTvApiKey = null);
