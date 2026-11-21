namespace WaveMP.Core.Subsonic.Requests;

/// <summary>
/// What a cover id of the Subsonic API belongs to.
/// </summary>
public enum SubsonicCoverOwner
{
    /// <summary>
    /// A plain id without prefix: a track, an album or a playlist.
    /// </summary>
    Any,

    Album,
    Track,
    Playlist,

    /// <summary>
    /// A smart playlist; its cover is the cover of one of its first songs.
    /// </summary>
    SmartPlaylist,
}
