namespace WaveMP.Core.Subsonic.Accounts;

/// <summary>
/// A new app password, shown to the user this once.
/// </summary>
/// <param name="Password">The app password in plain text.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="State">The Subsonic access of the user with the new password.</param>
public sealed record SubsonicPassword(string Password, DateTimeOffset CreatedAt, SubsonicAccountState State);
