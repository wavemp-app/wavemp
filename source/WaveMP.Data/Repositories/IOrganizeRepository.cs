using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Data access of the organization of the media files.
/// </summary>
public interface IOrganizeRepository
{
    /// <summary>
    /// The values of the given tracks for a layout template, in the order of <paramref name="trackIds"/>; unknown ids
    /// are left out.
    /// </summary>
    Task<IReadOnlyList<TemplateSongRow>> GetTemplateSongsAsync(
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The values of the first <paramref name="count"/> tracks of the library for a layout template, ordered by album
    /// artist, album, disc, track number and title; tracks with an album come first.
    /// </summary>
    Task<IReadOnlyList<TemplateSongRow>> GetSampleTemplateSongsAsync(
        int count,
        CancellationToken cancellationToken = default);
}
