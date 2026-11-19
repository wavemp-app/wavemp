namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The built-in templates of smart playlists; clients name them in the language of the user.
/// </summary>
public enum SmartPlaylistTemplateId
{
    /// <summary>
    /// Songs rated with 4 stars and more, the best first.
    /// </summary>
    FourStarsAndMore,

    /// <summary>
    /// Songs the user never played, the newest first.
    /// </summary>
    NeverPlayed,

    /// <summary>
    /// Songs found within the last 30 days, the newest first.
    /// </summary>
    RecentlyAdded,

    /// <summary>
    /// The 50 songs the user played most often.
    /// </summary>
    MostPlayed,

    /// <summary>
    /// The favorite songs of the user, by artist.
    /// </summary>
    Favorites,
}
