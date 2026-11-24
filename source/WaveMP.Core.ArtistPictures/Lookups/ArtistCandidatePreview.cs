namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The preview of a candidate as WebP.
/// </summary>
/// <param name="Status">Outcome; the content is only set with <see cref="ArtistLookupStatus.Found"/>.</param>
/// <param name="Content">The picture; the receiver disposes it.</param>
public sealed record ArtistCandidatePreview(ArtistLookupStatus Status, Stream? Content)
{
    public static ArtistCandidatePreview Failed(ArtistLookupStatus status)
    {
        return new ArtistCandidatePreview(status, null);
    }
}
