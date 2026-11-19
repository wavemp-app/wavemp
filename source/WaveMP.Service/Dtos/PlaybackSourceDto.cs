using System.ComponentModel.DataAnnotations;
using WaveMP.Service.Validators;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What a list in a player was started from: an album, an artist, a playlist or a smart playlist.
/// </summary>
public sealed class PlaybackSourceDto
{
    [Required]
    public required PlaybackSourceKindDto? Kind { get; init; }

    /// <summary>
    /// Id of the album, artist, playlist or smart playlist.
    /// </summary>
    [RequiredValue]
    public required Guid Id { get; init; }
}
