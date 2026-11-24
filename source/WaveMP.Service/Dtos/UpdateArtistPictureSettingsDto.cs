using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the settings of the artist pictures. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateArtistPictureSettingsDto
{
    /// <summary>
    /// Switches the online lookup of artist pictures on or off.
    /// </summary>
    public bool? OnlineLookupEnabled { get; init; }

    /// <summary>
    /// The personal API key of fanart.tv: 1 to 64 letters (A to Z) or digits, surrounding white space is removed. An
    /// empty string removes the stored key. Write-only: responses only say whether a key is stored.
    /// </summary>
    [StringLength(256)]
    public string? FanartTvApiKey { get; init; }
}
