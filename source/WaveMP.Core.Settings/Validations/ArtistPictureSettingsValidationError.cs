namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// A rejected value of the settings of the artist pictures. It never holds the value itself, since the API key is a
/// secret.
/// </summary>
public sealed record ArtistPictureSettingsValidationError(
    ArtistPictureSettingsField Field,
    SettingsErrorReason Reason);
