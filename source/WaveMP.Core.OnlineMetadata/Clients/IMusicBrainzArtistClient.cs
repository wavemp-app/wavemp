namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The artists of MusicBrainz for the online lookup of artist pictures. Requests wait in the shared queue of
/// MusicBrainz and are only sent while the lookup is switched on (<see cref="OnlineGate.ArtistPictures"/>).
/// </summary>
public interface IMusicBrainzArtistClient
{
    /// <summary>
    /// Searches artists by name (<c>GET ws/2/artist?query=artist:"…"</c>), the most relevant first.
    /// </summary>
    /// <param name="name">Name of the artist.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="limit">Number of results, 1 to 100.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<OnlineResponse<MusicBrainzArtistPage>> SearchAsync(
        string name,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The details of an artist that the online lookup uses, with one request
    /// (<c>GET ws/2/artist/{id}?inc=url-rels+aliases</c>): the id of its Wikidata item (e.g. <c>Q11649</c>) from its
    /// links, its sort name and its aliases; <see cref="OnlineStatus.NotFound"/> if MusicBrainz does not know the
    /// artist.
    /// </summary>
    Task<OnlineResponse<MusicBrainzArtistDetails>> GetArtistAsync(
        Guid artistId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What the online lookup uses of a MusicBrainz artist. All texts are plain text.
/// </summary>
/// <param name="WikidataItemId">The id of its Wikidata item; <c>null</c> without a Wikidata link.</param>
/// <param name="SortName">Name for sorting, e.g. "Beatles, The".</param>
/// <param name="Aliases">Further names of the artist: other spellings, names in other languages, legal names and
/// earlier names, each once.</param>
public sealed record MusicBrainzArtistDetails(string? WikidataItemId, string? SortName, IReadOnlyList<string> Aliases);

/// <summary>
/// A page of the artist search of MusicBrainz.
/// </summary>
/// <param name="Total">Number of all results of the search.</param>
/// <param name="Artists">The results of the page.</param>
public sealed record MusicBrainzArtistPage(int Total, IReadOnlyList<MusicBrainzArtistMatch> Artists);

/// <summary>
/// An artist of MusicBrainz that may be the artist of the library. All texts are plain text.
/// </summary>
/// <param name="Id">The MusicBrainz id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
/// <param name="SortName">Name for sorting, e.g. "Beatles, The".</param>
/// <param name="Disambiguation">Comment that tells artists of the same name apart.</param>
/// <param name="Type">Kind of artist as MusicBrainz names it, e.g. Person or Group.</param>
/// <param name="Country">Country code of the artist, e.g. GB.</param>
/// <param name="LifeSpanBegin">Begin of the life span or career (year, year-month or date).</param>
/// <param name="LifeSpanEnd">End of the life span or career.</param>
/// <param name="Score">Relevance of the search, 0 to 100.</param>
public sealed record MusicBrainzArtistMatch(
    Guid Id,
    string Name,
    string? SortName,
    string? Disambiguation,
    string? Type,
    string? Country,
    string? LifeSpanBegin,
    string? LifeSpanEnd,
    int Score);
