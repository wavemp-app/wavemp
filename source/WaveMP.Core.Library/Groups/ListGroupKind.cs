namespace WaveMP.Core.Library.Groups;

/// <summary>
/// What the entries of a group of a list have in common.
/// </summary>
public enum ListGroupKind
{
    /// <summary>
    /// The album: tracks of one album.
    /// </summary>
    Album,

    /// <summary>
    /// The artist: tracks without album of one artist, or albums of one album artist.
    /// </summary>
    Artist,

    /// <summary>
    /// The index letter of the text the list is sorted by (see <see cref="IndexLetters"/>).
    /// </summary>
    Letter,

    /// <summary>
    /// The year: albums of one year.
    /// </summary>
    Year,

    /// <summary>
    /// The value is unknown: tracks without album and artist, albums without album artist or without year.
    /// </summary>
    Unknown,
}
