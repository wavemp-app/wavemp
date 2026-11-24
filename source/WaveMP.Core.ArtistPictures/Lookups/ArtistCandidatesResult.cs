namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The pictures the online lookup found for an artist.
/// </summary>
/// <param name="Status">Outcome of the lookup; the candidates are only set with
/// <see cref="ArtistLookupStatus.Found"/>.</param>
/// <param name="Candidates">The pictures, those of Wikimedia Commons first, then those of fanart.tv by likes.</param>
public sealed record ArtistCandidatesResult(ArtistLookupStatus Status, IReadOnlyList<ArtistPictureCandidate> Candidates)
{
    public static ArtistCandidatesResult Failed(ArtistLookupStatus status)
    {
        return new ArtistCandidatesResult(status, []);
    }
}
