namespace WaveMP.Service.Dtos;

/// <summary>
/// A short-lived ticket that opens the real-time connection of the devices (asyncapi.yaml) for the current session.
/// Browsers cannot send a header when they open a WebSocket or an event stream, so the ticket goes into the query
/// parameter <c>access_token</c> instead of the session token: it ends after a minute and is accepted by nothing but
/// the connection of the devices, so a copy in an access log is of no use.
/// </summary>
public sealed class DeviceHubTicketDto
{
    /// <summary>
    /// The ticket (a signed token).
    /// </summary>
    public required string Ticket { get; init; }

    /// <summary>
    /// When the ticket ends; a connection must be opened (negotiated) before. An open connection stays open while its
    /// session is active.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
