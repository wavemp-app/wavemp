using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// Result of changing the settings of the artist pictures: the saved settings or the errors why nothing was saved.
/// </summary>
public sealed record ArtistPictureSettingsResult(
    ArtistPictureSettings? Settings,
    IReadOnlyList<ArtistPictureSettingsValidationError> Errors)
{
    [MemberNotNullWhen(true, nameof(Settings))]
    public bool IsSuccess => Settings is not null;

    public static ArtistPictureSettingsResult Success(ArtistPictureSettings settings)
    {
        return new ArtistPictureSettingsResult(settings, []);
    }

    public static ArtistPictureSettingsResult Failure(IReadOnlyList<ArtistPictureSettingsValidationError> errors)
    {
        return new ArtistPictureSettingsResult(null, errors);
    }
}
