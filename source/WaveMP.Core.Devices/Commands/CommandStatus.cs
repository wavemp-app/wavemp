namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// Outcome of a command, a hand-over or a change of the permission.
/// </summary>
public enum CommandStatus
{
    /// <summary>
    /// The command was accepted and passed on.
    /// </summary>
    Accepted,

    /// <summary>
    /// The connection has not registered a device (or another connection of the device replaced it).
    /// </summary>
    NotRegistered,

    /// <summary>
    /// The profile has no open device with this id.
    /// </summary>
    DeviceNotFound,

    /// <summary>
    /// The device has not allowed remote control; nothing was passed on.
    /// </summary>
    RemoteControlNotAllowed,

    /// <summary>
    /// The target is not the active device of the profile; commands only go to the device that plays.
    /// </summary>
    NotActive,

    /// <summary>
    /// The state changed since the version the device saw; it loads the state again and decides anew.
    /// </summary>
    Conflict,

    /// <summary>
    /// The command is invalid; the errors name the fields.
    /// </summary>
    Invalid,
}
