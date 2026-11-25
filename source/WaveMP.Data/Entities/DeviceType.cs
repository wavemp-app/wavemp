namespace WaveMP.Data.Entities;

/// <summary>
/// Kind of a device a profile uses WaveMP on.
/// </summary>
public enum DeviceType
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
