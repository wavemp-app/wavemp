namespace WaveMP.Core.Subsonic.NowPlaying;

/// <summary>
/// A song a user started to play.
/// </summary>
/// <param name="UserId">Id of the profile.</param>
/// <param name="TrackId">Id of the track.</param>
/// <param name="PlayerName">Name of the app (the parameter <c>c</c> of the Subsonic API); <c>null</c> if unknown.
/// </param>
/// <param name="StartedAt">When the app reported the song.</param>
public sealed record SubsonicNowPlayingEntry(Guid UserId, Guid TrackId, string? PlayerName, DateTimeOffset StartedAt);
