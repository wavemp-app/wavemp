namespace WaveMP.Data.Entities;

/// <summary>
/// A recap a user shared with the other profiles of the instance: the period of its play history and the design the
/// recap is shown in. The statistics are not stored; they are computed from the play history of the user whenever the
/// recap is opened, so tracks that left the library are left out. A user shares each period once; sharing it again
/// changes the design. The share disappears with its user.
/// </summary>
public class RecapShare
{
    /// <summary>
    /// Longest id of a design in <see cref="Design"/>.
    /// </summary>
    public const int DesignMaxLength = 32;

    /// <summary>
    /// Longest IANA time zone in <see cref="TimeZone"/>.
    /// </summary>
    public const int TimeZoneMaxLength = 64;

    public Guid Id { get; set; }

    public required Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// How the period was chosen. Stored as its name.
    /// </summary>
    public required RecapPeriod Period { get; set; }

    /// <summary>
    /// First day of the period in <see cref="TimeZone"/>; <c>null</c> from the first play on.
    /// </summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>
    /// Last day of the period in <see cref="TimeZone"/>.
    /// </summary>
    public required DateOnly ToDate { get; set; }

    /// <summary>
    /// IANA time zone the days are counted in, e.g. <c>Europe/Berlin</c>.
    /// </summary>
    public required string TimeZone { get; set; }

    /// <summary>
    /// Id of the design of the clients, e.g. <c>aurora</c>; the server does not interpret it.
    /// </summary>
    public required string Design { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the recap was shared for the last time (with its current design).
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; set; }
}
