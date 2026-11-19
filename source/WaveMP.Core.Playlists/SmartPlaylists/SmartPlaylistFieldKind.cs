namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The kind of value of a field of a smart playlist rule; it decides the operators and the value of the rule.
/// </summary>
public enum SmartPlaylistFieldKind
{
    /// <summary>
    /// Artist, album, genre: Is, IsNot, Contains, DoesNotContain with a text.
    /// </summary>
    Text,

    /// <summary>
    /// Year, rating, play count, duration: Is, IsNot, AtLeast, AtMost with a number, Between with two.
    /// </summary>
    Number,

    /// <summary>
    /// Last played, date added: InLast, NotInLast with a number of days.
    /// </summary>
    Date,

    /// <summary>
    /// Favorite, audio, video: IsTrue, IsFalse without a value.
    /// </summary>
    Flag,
}
