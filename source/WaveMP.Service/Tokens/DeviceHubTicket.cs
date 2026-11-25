namespace WaveMP.Service.Tokens;

/// <summary>
/// A short-lived ticket of the real-time connection of the devices.
/// </summary>
/// <param name="Ticket">The ticket, a signed token.</param>
/// <param name="ExpiresAt">When it ends.</param>
public sealed record DeviceHubTicket(string Ticket, DateTimeOffset ExpiresAt);
