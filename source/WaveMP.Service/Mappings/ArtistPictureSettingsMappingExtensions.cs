using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ArtistPictureSettingsMappingExtensions
{
    /// <summary>
    /// The settings for the API: whether an API key is stored, never the key.
    /// </summary>
    public static ArtistPictureSettingsDto ToDto(this ArtistPictureSettings settings)
    {
        return new ArtistPictureSettingsDto
        {
            OnlineLookupEnabled = settings.OnlineLookupEnabled,
            FanartTvApiKeySet = settings.FanartTvApiKey is not null,
        };
    }

    public static UpdateArtistPictureSettingsCommand ToCommand(this UpdateArtistPictureSettingsDto request)
    {
        return new UpdateArtistPictureSettingsCommand(request.OnlineLookupEnabled, request.FanartTvApiKey);
    }

    /// <summary>
    /// The JSON name of the field in <see cref="UpdateArtistPictureSettingsDto"/>, the key of its validation errors.
    /// </summary>
    public static string ToJsonName(this ArtistPictureSettingsField field)
    {
        return field switch
        {
            ArtistPictureSettingsField.FanartTvApiKey => "fanartTvApiKey",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }
}
