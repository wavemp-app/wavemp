namespace WaveMP.Data.Entities;

/// <summary>
/// The account of a user at a scrobbling service, at most one per user and service. Removed with its user; its queued
/// scrobbles are removed with it.
/// </summary>
public class ScrobbleConnection
{
    public Guid Id { get; set; }

    public required Guid UserId { get; set; }

    public User? User { get; set; }

    public required ScrobbleService Service { get; set; }

    /// <summary>
    /// Name of the account at the service, as the service reports it.
    /// </summary>
    public required string AccountName { get; set; }

    /// <summary>
    /// The credentials (the session key of Last.fm, the user token of ListenBrainz), encrypted with ASP.NET Core Data
    /// Protection; never stored in plain text.
    /// </summary>
    public required string ProtectedCredential { get; set; }

    public required ScrobbleConnectionStatus Status { get; set; }

    /// <summary>
    /// When the service was connected the last time.
    /// </summary>
    public required DateTimeOffset ConnectedAt { get; set; }

    /// <summary>
    /// When the service accepted scrobbles the last time; <c>null</c> before the first.
    /// </summary>
    public DateTimeOffset? LastScrobbledAt { get; set; }
}
