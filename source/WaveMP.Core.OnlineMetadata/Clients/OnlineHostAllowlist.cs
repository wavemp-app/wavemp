namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The addresses the online lookup of artist pictures may request, also as target of a redirect: https addresses on
/// the standard port of Wikidata, Wikimedia Commons, its picture server and fanart.tv, plus the address of MusicBrainz
/// the admin configured (with its scheme and port, e.g. an own mirror). Everything else is refused, so an answer of a
/// service can never make WaveMP request an address in the local network.
/// </summary>
public static class OnlineHostAllowlist
{
    public const string Wikidata = "www.wikidata.org";
    public const string WikimediaCommons = "commons.wikimedia.org";
    public const string WikimediaUpload = "upload.wikimedia.org";
    public const string FanartTvService = "webservice.fanart.tv";
    public const string FanartTvAssets = "assets.fanart.tv";

    /// <summary>
    /// The hosts that are allowed with https on the standard port.
    /// </summary>
    public static IReadOnlySet<string> Hosts { get; } = new HashSet<string>(
        [Wikidata, WikimediaCommons, WikimediaUpload, FanartTvService, FanartTvAssets],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="url"/> may be requested: an https address on the standard port of one of
    /// <see cref="Hosts"/>, or an address of the origin (scheme, host and port) of <paramref name="musicBrainzUrl"/>.
    /// Addresses with user info are never allowed.
    /// </summary>
    public static bool IsAllowed(Uri url, string? musicBrainzUrl)
    {
        if (!url.IsAbsoluteUri || url.UserInfo.Length > 0)
        {
            return false;
        }

        if (musicBrainzUrl is not null
            && Uri.TryCreate(musicBrainzUrl, UriKind.Absolute, out Uri? musicBrainz)
            && (musicBrainz.Scheme == Uri.UriSchemeHttps || musicBrainz.Scheme == Uri.UriSchemeHttp)
            && musicBrainz.UserInfo.Length == 0
            && string.Equals(url.Scheme, musicBrainz.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(HostOf(url), HostOf(musicBrainz), StringComparison.OrdinalIgnoreCase)
            && url.Port == musicBrainz.Port)
        {
            return true;
        }

        return url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort && Hosts.Contains(HostOf(url));
    }

    private static string HostOf(Uri url)
    {
        return url.IdnHost.TrimEnd('.');
    }
}
