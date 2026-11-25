using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Kind of a device; clients show an icon for it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<DeviceTypeDto>))]
public enum DeviceTypeDto
{
    /// <summary>
    /// A desktop or laptop computer.
    /// </summary>
    Desktop,

    /// <summary>
    /// A smartphone.
    /// </summary>
    Phone,

    /// <summary>
    /// A tablet.
    /// </summary>
    Tablet,

    /// <summary>
    /// A TV or set-top box.
    /// </summary>
    Tv,

    /// <summary>
    /// A watch.
    /// </summary>
    Watch,

    /// <summary>
    /// Any other device.
    /// </summary>
    Other,
}
