namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The switch a request to an online service depends on. The sender checks it immediately before every request it
/// sends, also before every further attempt and redirect, so switching a feature off stops its requests at once.
/// </summary>
public enum OnlineGate
{
    /// <summary>
    /// The online search for album info (<see cref="Settings.Groups.OnlineMetadataSettings.Enabled"/>).
    /// </summary>
    AlbumSearch,

    /// <summary>
    /// The online lookup of artist pictures
    /// (<see cref="Settings.Groups.ArtistPictureSettings.OnlineLookupEnabled"/>). Its requests never follow redirects
    /// on their own: every address must be an https address of an allowed host (see
    /// <see cref="OnlineHostAllowlist"/>).
    /// </summary>
    ArtistPictures,
}
