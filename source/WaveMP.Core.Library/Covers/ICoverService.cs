using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Finds the covers of tracks and albums following the cover rules (see <see cref="CoverResolver"/> and
/// <see cref="CoverRules"/>): for tracks the most specific rule of the songs (of the track, of its album, otherwise the
/// global one of the library settings), for albums their own covers and whether they may fall back to the picture of
/// their album artist. Reading the pictures is the task of <see cref="ICoverSourceReader"/>.
/// </summary>
public interface ICoverService
{
    /// <summary>
    /// The covers that may be shown for a track, the best first: its own cover and the cover of its album in the order
    /// of its rule.
    /// </summary>
    Task<CoverCandidates> GetTrackCandidatesAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The own covers of an album, the best first, and the artist whose picture it shows without one, if its rule
    /// allows that.
    /// </summary>
    Task<CoverCandidates> GetAlbumCandidatesAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers a track would show with another rule of its own, the best first; <paramref name="rule"/>
    /// <c>null</c> inherits the rule of its album. Nothing is changed.
    /// </summary>
    Task<CoverCandidates> PreviewTrackCandidatesAsync(
        Guid trackId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers an album shows; the rule of its songs (<paramref name="rule"/>) does not change the cover of the
    /// album itself, so this is the same as <see cref="GetAlbumCandidatesAsync"/>. Nothing is changed.
    /// </summary>
    Task<CoverCandidates> PreviewAlbumCandidatesAsync(
        Guid albumId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The ordered lists of covers that the library shows: per album the list of the album, of every track with covers
    /// of its own and of its tracks without one, then the list of every track without album. Identical lists come
    /// only once, empty lists never. Reads the library in batches. The pictures of artists are prepared by their own.
    /// </summary>
    IAsyncEnumerable<IReadOnlyList<CoverCandidateRow>> EnumerateShownCoversAsync(
        CancellationToken cancellationToken = default);
}
