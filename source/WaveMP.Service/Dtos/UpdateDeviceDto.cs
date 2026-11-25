using System.ComponentModel.DataAnnotations;
using WaveMP.Data.Entities;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes a device of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateDeviceDto
{
    /// <summary>
    /// The name the user gives the device, 1 to 100 characters without control characters; spaces at the ends are
    /// removed. The name the device reports for itself removes the given name.
    /// </summary>
    [StringLength(Device.NameMaxLength, MinimumLength = 1)]
    public string? Name { get; init; }
}
