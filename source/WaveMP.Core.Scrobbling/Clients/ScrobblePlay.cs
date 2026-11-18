namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// A play to scrobble: the song and when its playback started.
/// </summary>
public sealed record ScrobblePlay(ScrobbleTrack Track, DateTimeOffset PlayedAt);
