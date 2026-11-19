namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The reason why a field of a smart playlist was rejected.
/// </summary>
public enum SmartPlaylistErrorCode
{
    /// <summary>
    /// The value is missing (also a text of spaces only).
    /// </summary>
    Required,

    /// <summary>
    /// The value is not allowed here, e.g. an operator that does not fit the field or an invalid name.
    /// </summary>
    InvalidValue,

    /// <summary>
    /// The number lies outside its range.
    /// </summary>
    OutOfRange,

    /// <summary>
    /// The text is too long.
    /// </summary>
    MaxLength,

    /// <summary>
    /// There are too many rules.
    /// </summary>
    MaxItems,
}
