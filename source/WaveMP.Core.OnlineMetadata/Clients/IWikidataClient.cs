namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Wikidata for the online lookup of artist pictures, without API key. Requests are sent one after the other and only
/// while the lookup is switched on (<see cref="OnlineGate.ArtistPictures"/>).
/// </summary>
public interface IWikidataClient
{
    /// <summary>
    /// The file names on Wikimedia Commons of the pictures of an item (property P18, image), preferred ones first,
    /// without deprecated ones (<c>GET w/api.php?action=wbgetentities</c>). An empty list for an item without picture,
    /// <see cref="OnlineStatus.NotFound"/> for an unknown item.
    /// </summary>
    /// <param name="itemId">Id of the item, e.g. <c>Q11649</c>.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<OnlineResponse<IReadOnlyList<string>>> GetImageFileNamesAsync(
        string itemId,
        CancellationToken cancellationToken = default);
}
