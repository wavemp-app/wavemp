namespace WaveMP.Service.Dtos;

/// <summary>
/// A release of MusicBrainz with what tells it apart from other releases of the same album.
/// </summary>
public sealed class OnlineReleaseDto
{
    /// <summary>
    /// The MusicBrainz id (MBID) of the release; its cover comes from <c>GET /api/v1/online-releases/{id}/cover</c>.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Date of the release: <c>YYYY</c>, <c>YYYY-MM</c> or <c>YYYY-MM-DD</c>.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// Country of the release as ISO 3166-1 code, e.g. <c>GB</c>; <c>XW</c> stands for worldwide.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Format of its first medium as MusicBrainz names it, e.g. <c>CD</c> or <c>Digital Media</c>.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Number of tracks on all media.
    /// </summary>
    public int? TrackCount { get; init; }
}
