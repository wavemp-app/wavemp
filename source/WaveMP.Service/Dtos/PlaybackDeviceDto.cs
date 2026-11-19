using System.ComponentModel.DataAnnotations;
using WaveMP.Data.Entities;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The device that changed a playback state.
/// </summary>
public sealed class PlaybackDeviceDto
{
    /// <summary>
    /// Id the device chose for itself and keeps, 1 to 64 characters, so it recognizes its own state.
    /// </summary>
    [Required]
    [StringLength(PlaybackState.DeviceIdMaxLength, MinimumLength = 1)]
    public required string Id { get; init; }

    /// <summary>
    /// Name of the device that other devices show ("continue listening from"), 1 to 100 characters, for example
    /// "Firefox · Windows".
    /// </summary>
    [Required]
    [StringLength(PlaybackState.DeviceNameMaxLength, MinimumLength = 1)]
    public required string Name { get; init; }
}
