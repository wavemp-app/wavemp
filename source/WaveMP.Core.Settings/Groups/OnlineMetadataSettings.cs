namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Settings of the online search for album info: tags from MusicBrainz and covers from the Cover Art Archive. Off by
/// default; while it is off, WaveMP sends no request to either service. Stored as the settings group <see cref="Key"/>;
/// values missing in the stored JSON keep the defaults below. Addresses are stored without a trailing slash.
/// </summary>
public sealed class OnlineMetadataSettings
{
    /// <summary>
    /// Key of the settings group.
    /// </summary>
    public const string Key = "online-metadata";

    public const string DefaultMusicBrainzUrl = "https://musicbrainz.org";
    public const string DefaultCoverArtArchiveUrl = "https://coverartarchive.org";

    /// <summary>
    /// Shortest time between two requests to musicbrainz.org itself, as its terms of use ask for (one per second).
    /// </summary>
    public const int OfficialMusicBrainzIntervalMilliseconds = 1000;

    /// <summary>
    /// Whether admins may search album info online. Off by default, because it sends titles, artists and albums of
    /// the library to the services.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Address of MusicBrainz, e.g. an own mirror; no API key is needed, because access is read-only.
    /// </summary>
    public string MusicBrainzUrl { get; init; } = DefaultMusicBrainzUrl;

    /// <summary>
    /// Address of the Cover Art Archive.
    /// </summary>
    public string CoverArtArchiveUrl { get; init; } = DefaultCoverArtArchiveUrl;

    /// <summary>
    /// Shortest time between two requests to MusicBrainz in milliseconds. Applies to an own mirror; musicbrainz.org
    /// itself always gets at least <see cref="OfficialMusicBrainzIntervalMilliseconds"/>.
    /// </summary>
    public int MusicBrainzRequestIntervalMilliseconds { get; init; } = OfficialMusicBrainzIntervalMilliseconds;

    /// <summary>
    /// The time between two requests to MusicBrainz that applies: the setting, but at least one second for
    /// musicbrainz.org and its subdomains. A method, so that it is not stored with the settings.
    /// </summary>
    public TimeSpan GetMusicBrainzRequestInterval()
    {
        int milliseconds = Math.Max(0, MusicBrainzRequestIntervalMilliseconds);
        if (IsOfficialMusicBrainz(MusicBrainzUrl))
        {
            milliseconds = Math.Max(milliseconds, OfficialMusicBrainzIntervalMilliseconds);
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// Whether the address belongs to musicbrainz.org (also a subdomain such as beta.musicbrainz.org).
    /// </summary>
    public static bool IsOfficialMusicBrainz(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return true;
        }

        string host = uri.IdnHost.TrimEnd('.');
        return host.Equals("musicbrainz.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".musicbrainz.org", StringComparison.OrdinalIgnoreCase);
    }
}
