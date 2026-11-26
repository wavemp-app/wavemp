using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// What an album shows without a cover of its own, or that the album does not exist.
/// </summary>
/// <param name="IsFound">Whether the album exists.</param>
/// <param name="Fallback">The exception of the album; <c>null</c> if it inherits the global rule.</param>
/// <param name="Inherited">The global rule of the library settings.</param>
public sealed record AlbumCoverFallbackResult(bool IsFound, AlbumCoverFallback? Fallback, AlbumCoverFallback Inherited)
{
    public static AlbumCoverFallbackResult NotFound { get; } = new(false, null, AlbumCoverFallback.ArtistPicture);

    /// <summary>
    /// The rule that applies: its own, otherwise the inherited one.
    /// </summary>
    public AlbumCoverFallback Effective => Fallback ?? Inherited;
}
