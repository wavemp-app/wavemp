namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// fanart.tv for the online lookup of artist pictures, with the personal API key of the admin. The key is only sent in
/// the header <c>api-key</c> to <c>webservice.fanart.tv</c>, never in an address and never to another host. Requests
/// are sent one after the other and only while the lookup is switched on (<see cref="OnlineGate.ArtistPictures"/>).
/// </summary>
public interface IFanartTvClient
{
    /// <summary>
    /// The artist pictures (artistthumb) of an artist, the most liked first (<c>GET v3/music/{id}</c>); an empty list
    /// if fanart.tv does not know the artist.
    /// </summary>
    /// <param name="musicBrainzArtistId">The MusicBrainz id of the artist.</param>
    /// <param name="apiKey">The API key of the admin.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<OnlineResponse<IReadOnlyList<FanartTvPicture>>> GetArtistPicturesAsync(
        Guid musicBrainzArtistId,
        string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a picture of fanart.tv (an address of <see cref="GetArtistPicturesAsync"/>).
    /// </summary>
    Task<OnlineResponse<byte[]>> DownloadAsync(Uri url, CancellationToken cancellationToken = default);
}

/// <summary>
/// An artist picture of fanart.tv.
/// </summary>
/// <param name="Id">Id of the picture at fanart.tv.</param>
/// <param name="Url">Address of the picture.</param>
/// <param name="Likes">Number of likes of the users of fanart.tv.</param>
public sealed record FanartTvPicture(string Id, Uri Url, int Likes);
