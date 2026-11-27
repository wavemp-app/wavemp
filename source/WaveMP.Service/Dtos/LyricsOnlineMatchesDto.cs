namespace WaveMP.Service.Dtos;

/// <summary>
/// The tracks with lyrics a search in the online lyrics service (LRCLIB) found for a track, the best first, and what
/// was searched for. LRCLIB returns at most 20 tracks, so the list is not paged.
/// </summary>
public sealed class LyricsOnlineMatchesDto
{
    /// <summary>
    /// The title that was searched for.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The artist that was searched for, or <c>null</c> for a search by title only.
    /// </summary>
    public string? Artist { get; init; }

    /// <summary>
    /// The tracks with lyrics: those with the searched names first, then those whose duration fits the track, then
    /// those with synchronized lyrics.
    /// </summary>
    public required IReadOnlyList<LyricsOnlineMatchDto> Matches { get; init; }
}
