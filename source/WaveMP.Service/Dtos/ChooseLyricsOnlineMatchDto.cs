using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The track of the online lyrics service (LRCLIB) whose lyrics a track gets.
/// </summary>
public sealed class ChooseLyricsOnlineMatchDto
{
    /// <summary>
    /// Id of the track at LRCLIB, from the search (<c>GET /api/v1/tracks/{id}/lyrics/online-matches</c>).
    /// </summary>
    [Range(1, long.MaxValue)]
    public required long MatchId { get; init; }
}
