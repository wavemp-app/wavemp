using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// The account of a user at a scrobbling service, or why there is none.
/// </summary>
/// <param name="Status">Whether the token was accepted.</param>
/// <param name="AccountName">Name of the account; set if authorized.</param>
/// <param name="Credential">What WaveMP keeps to send in the name of the user (the session key of Last.fm, the
/// token of ListenBrainz); set if authorized.</param>
public sealed record ScrobbleAuthorization(
    ScrobbleAuthorizationStatus Status,
    string? AccountName = null,
    string? Credential = null)
{
    public static ScrobbleAuthorization Invalid { get; } = new(ScrobbleAuthorizationStatus.Invalid);

    public static ScrobbleAuthorization Unavailable { get; } = new(ScrobbleAuthorizationStatus.Unavailable);

    [MemberNotNullWhen(true, nameof(AccountName), nameof(Credential))]
    public bool IsAuthorized =>
        Status == ScrobbleAuthorizationStatus.Authorized && AccountName is not null && Credential is not null;

    public static ScrobbleAuthorization Authorized(string accountName, string credential)
    {
        return new ScrobbleAuthorization(ScrobbleAuthorizationStatus.Authorized, accountName, credential);
    }
}
