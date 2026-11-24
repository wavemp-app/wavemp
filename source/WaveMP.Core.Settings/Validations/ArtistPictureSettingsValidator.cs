using System.Text.RegularExpressions;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// Normalizes and checks the settings of the artist pictures before they are saved.
/// </summary>
public static partial class ArtistPictureSettingsValidator
{
    public const int MaxApiKeyLength = 64;

    /// <summary>
    /// The settings as they are stored: the API key trimmed, and <c>null</c> when it is empty.
    /// </summary>
    public static ArtistPictureSettings Normalize(ArtistPictureSettings settings)
    {
        string? key = settings.FanartTvApiKey?.Trim();
        return new ArtistPictureSettings
        {
            OnlineLookupEnabled = settings.OnlineLookupEnabled,
            FanartTvApiKey = string.IsNullOrEmpty(key) ? null : key,
        };
    }

    /// <summary>
    /// All errors of normalized settings; an empty list means they are valid.
    /// </summary>
    public static IReadOnlyList<ArtistPictureSettingsValidationError> Validate(ArtistPictureSettings settings)
    {
        List<ArtistPictureSettingsValidationError> errors = [];
        if (settings.FanartTvApiKey is { } key && !IsValidApiKey(key))
        {
            errors.Add(new(ArtistPictureSettingsField.FanartTvApiKey, SettingsErrorReason.Invalid));
        }

        return errors;
    }

    /// <summary>
    /// An API key of fanart.tv: 1 to <see cref="MaxApiKeyLength"/> letters (A to Z) or digits. Keeps anything that
    /// could change a request header or a log line out.
    /// </summary>
    public static bool IsValidApiKey(string key)
    {
        return ApiKeyPattern().IsMatch(key);
    }

    [GeneratedRegex("^[A-Za-z0-9]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyPattern();
}
