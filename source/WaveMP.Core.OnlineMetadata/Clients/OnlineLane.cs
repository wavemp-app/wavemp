namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Services whose requests are sent strictly one after the other, as their terms ask for (MusicBrainz has its own
/// queue, <see cref="MusicBrainzRequestQueue"/>).
/// </summary>
internal enum OnlineLane
{
    /// <summary>
    /// Wikidata, Wikimedia Commons and its pictures.
    /// </summary>
    Wikimedia,

    /// <summary>
    /// fanart.tv and its pictures.
    /// </summary>
    FanartTv,
}
