namespace WaveMP.Data.Entities;

/// <summary>
/// An online service that played songs are reported to (scrobbling).
/// </summary>
public enum ScrobbleService
{
    /// <summary>
    /// Last.fm, connected through its web authentication.
    /// </summary>
    LastFm,

    /// <summary>
    /// ListenBrainz, connected with the personal user token.
    /// </summary>
    ListenBrainz,
}
