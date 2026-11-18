using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Connects a scrobbling service to the current profile.
/// </summary>
public sealed class ConnectScrobblingServiceDto
{
    /// <summary>
    /// Last.fm: the token Last.fm appended to the callback URL after the user allowed access. ListenBrainz: the
    /// personal user token from the settings of ListenBrainz. 1 to 256 characters.
    /// </summary>
    [Required]
    [StringLength(256)]
    public required string Token { get; init; }
}
