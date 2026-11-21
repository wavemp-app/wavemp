namespace WaveMP.Service.Dtos;

/// <summary>
/// The stored result of the last lookup of the lyrics of a track in the online lyrics service (LRCLIB).
/// </summary>
public sealed class LyricsLookupDto
{
    /// <summary>
    /// What the lookup found.
    /// </summary>
    public required LyricsLookupStatusDto Status { get; init; }

    /// <summary>
    /// When the track was looked up.
    /// </summary>
    public required DateTimeOffset CheckedAt { get; init; }

    /// <summary>
    /// Whether the result can be forgotten (<c>DELETE /api/v1/tracks/{id}/lyrics/lookup</c>): only lookups that found
    /// no lyrics (not found or instrumental).
    /// </summary>
    public required bool IsForgettable { get; init; }
}
