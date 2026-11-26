using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The covers that may be shown for a track or an album, the best first, or that the track or album does not exist.
/// </summary>
/// <param name="OwnerExists">Whether the track or album exists.</param>
/// <param name="Ordered">The covers ordered by the cover rule; the first one that can be read is shown.</param>
/// <param name="FallbackArtistId">For an album that may show the picture of its album artist when none of its own
/// covers can be read: the id of that artist; <c>null</c> otherwise and for tracks. Only a real picture of the artist
/// counts (in its folder, uploaded or taken over from the online lookup), never the collage of its album covers.
/// </param>
public sealed record CoverCandidates(
    bool OwnerExists,
    IReadOnlyList<CoverCandidateRow> Ordered,
    Guid? FallbackArtistId = null)
{
    public static CoverCandidates OwnerNotFound { get; } = new(false, []);
}
