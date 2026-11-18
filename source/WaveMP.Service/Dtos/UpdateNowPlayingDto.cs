using WaveMP.Service.Validators;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The track the current profile starts to listen to.
/// </summary>
public sealed class UpdateNowPlayingDto
{
    /// <summary>
    /// Id of the track.
    /// </summary>
    [RequiredValue]
    public required Guid TrackId { get; init; }
}
