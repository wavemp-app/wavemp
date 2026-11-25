namespace WaveMP.Data.Entities;

/// <summary>
/// A device a profile has used WaveMP on, one row per profile and device id: the name the device reports, the name
/// the user gave it, its kind and when it was last active. Whether a device is open right now, allows remote control
/// or plays is not stored; the server keeps that in memory while the device is connected. The devices disappear with
/// their profile.
/// </summary>
public class Device
{
    /// <summary>
    /// Longest id of a device, the same as the id of a device in a playback state.
    /// </summary>
    public const int IdMaxLength = PlaybackState.DeviceIdMaxLength;

    /// <summary>
    /// Longest name of a device, reported or given by the user.
    /// </summary>
    public const int NameMaxLength = PlaybackState.DeviceNameMaxLength;

    public required Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Id the device chose for itself and keeps (the web app keeps it in the local storage of the browser).
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Name the device reports for itself, e.g. "Firefox · Windows"; updated on every registration.
    /// </summary>
    public required string ReportedName { get; set; }

    /// <summary>
    /// Name the user gave the device; <c>null</c> shows <see cref="ReportedName"/>.
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// Kind of the device as it reports it. Stored as its name.
    /// </summary>
    public required DeviceType Type { get; set; }

    /// <summary>
    /// When the device registered for the first time.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the device was last active: registered, played, sent a command or closed its connection.
    /// </summary>
    public required DateTimeOffset LastActiveAt { get; set; }
}
