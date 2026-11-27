using WaveMP.Data.Projections;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The outcome of taking the aliases of an artist from MusicBrainz.
/// </summary>
/// <param name="Status">Outcome of the lookup; <see cref="ArtistLookupStatus.MusicBrainzArtistMissing"/> when the
/// artist has no MusicBrainz artist.</param>
/// <param name="Aliases">All aliases of the artist afterwards; only set with <see cref="ArtistLookupStatus.Found"/>.
/// </param>
public sealed record ArtistAliasLookupResult(ArtistLookupStatus Status, IReadOnlyList<ArtistAliasRow> Aliases)
{
    public static ArtistAliasLookupResult Failed(ArtistLookupStatus status)
    {
        return new ArtistAliasLookupResult(status, []);
    }
}
