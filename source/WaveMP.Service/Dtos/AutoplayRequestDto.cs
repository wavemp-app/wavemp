using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Service.Controllers;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Asks for the songs autoplay plays after a list.
/// </summary>
public sealed class AutoplayRequestDto
{
    /// <summary>
    /// The tracks of the list that ended or is about to end (album, playlist or queue), in their order, 1 to 500; for a
    /// longer list its last tracks. The choice follows their artists, genres and years; they are never chosen
    /// themselves. Unknown ids are skipped.
    /// </summary>
    [Required]
    [Length(1, AutoplayLimits.MaxListTracks)]
    public required IReadOnlyList<Guid> TrackIds { get; init; }

    /// <summary>
    /// Further tracks that must not be chosen, at most 500, e.g. the songs that already wait in the autoplay list of
    /// the client.
    /// </summary>
    [MaxLength(AutoplayLimits.MaxExcludedTracks)]
    public IReadOnlyList<Guid>? ExcludedTrackIds { get; init; }

    /// <summary>
    /// Number of songs wanted, from 1 to 50; 10 if missing.
    /// </summary>
    [Range(1, AutoplayLimits.MaxCount)]
    public int? Count { get; init; }

    /// <summary>
    /// The audio formats the client plays, 1 to 20, as for GetTrackPlayback: they decide the playback of each song.
    /// </summary>
    [Required]
    [Length(1, TrackStreamsController.MaxAudioFormats)]
    public required IReadOnlyList<AudioFormatDto> AudioFormats { get; init; }
}
