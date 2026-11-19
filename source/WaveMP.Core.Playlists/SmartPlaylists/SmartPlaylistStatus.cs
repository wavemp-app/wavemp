namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// Outcome of a change of a smart playlist.
/// </summary>
public enum SmartPlaylistStatus
{
    Ok,

    /// <summary>
    /// The smart playlist does not exist or belongs to another user.
    /// </summary>
    NotFound,

    /// <summary>
    /// At least one field is not valid; nothing was changed.
    /// </summary>
    Invalid,

    /// <summary>
    /// The smart playlist holds more tracks than a playlist may have; it was not converted.
    /// </summary>
    Full,

    /// <summary>
    /// The library kept changing while the smart playlist was converted (e.g. during a scan); nothing was changed.
    /// </summary>
    LibraryChanged,
}
