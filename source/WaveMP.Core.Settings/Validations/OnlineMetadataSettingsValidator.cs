using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// Normalizes and checks the settings of the online search before they are saved.
/// </summary>
public static class OnlineMetadataSettingsValidator
{
    public const int MinIntervalMilliseconds = 0;
    public const int MaxIntervalMilliseconds = 60_000;
    public const int MaxUrlLength = 2048;

    /// <summary>
    /// The settings as they are stored: addresses trimmed and without a trailing slash.
    /// </summary>
    public static OnlineMetadataSettings Normalize(OnlineMetadataSettings settings)
    {
        return new OnlineMetadataSettings
        {
            Enabled = settings.Enabled,
            MusicBrainzUrl = NormalizeUrl(settings.MusicBrainzUrl),
            CoverArtArchiveUrl = NormalizeUrl(settings.CoverArtArchiveUrl),
            MusicBrainzRequestIntervalMilliseconds = settings.MusicBrainzRequestIntervalMilliseconds,
        };
    }

    /// <summary>
    /// All errors of the settings; an empty list means they are valid.
    /// </summary>
    public static IReadOnlyList<OnlineMetadataSettingsValidationError> Validate(OnlineMetadataSettings settings)
    {
        List<OnlineMetadataSettingsValidationError> errors = [];
        if (!IsValidUrl(settings.MusicBrainzUrl))
        {
            errors.Add(new(OnlineMetadataSettingsField.MusicBrainzUrl, SettingsErrorReason.Invalid));
        }

        if (!IsValidUrl(settings.CoverArtArchiveUrl))
        {
            errors.Add(new(OnlineMetadataSettingsField.CoverArtArchiveUrl, SettingsErrorReason.Invalid));
        }

        if (settings.MusicBrainzRequestIntervalMilliseconds is < MinIntervalMilliseconds or > MaxIntervalMilliseconds)
        {
            errors.Add(new(
                OnlineMetadataSettingsField.MusicBrainzRequestIntervalMilliseconds,
                SettingsErrorReason.OutOfRange,
                MinIntervalMilliseconds,
                MaxIntervalMilliseconds));
        }

        return errors;
    }

    /// <summary>
    /// An absolute http or https address without user name, query or fragment, at most
    /// <see cref="MaxUrlLength"/> characters long.
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        return url is { Length: > 0 and <= MaxUrlLength }
            && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.UserInfo.Length == 0
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && uri.Host.Length > 0;
    }

    private static string NormalizeUrl(string? url)
    {
        return (url ?? string.Empty).Trim().TrimEnd('/');
    }
}
