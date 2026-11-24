using WaveMP.Core.OnlineMetadata.Clients;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The MusicBrainz artists that may be an artist of the library.
/// </summary>
/// <param name="Status">Outcome of the search; the other values are only set with
/// <see cref="ArtistLookupStatus.Found"/>.</param>
/// <param name="Matches">The artists of the page, the most relevant first.</param>
/// <param name="Total">Number of all results of the search.</param>
/// <param name="RememberedId">The MusicBrainz artist an admin chose earlier for the artist.</param>
public sealed record ArtistMatchesResult(
    ArtistLookupStatus Status,
    IReadOnlyList<MusicBrainzArtistMatch> Matches,
    int Total,
    Guid? RememberedId)
{
    public static ArtistMatchesResult Failed(ArtistLookupStatus status)
    {
        return new ArtistMatchesResult(status, [], 0, null);
    }
}
