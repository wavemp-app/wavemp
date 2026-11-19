namespace WaveMP.Service.Dtos;

/// <summary>
/// The result of applying a match to a song.
/// </summary>
public sealed class OnlineTrackApplyResultDto
{
    /// <summary>
    /// The metadata of the song read again after writing.
    /// </summary>
    public required TrackMetadataDto Metadata { get; init; }

    /// <summary>
    /// Where the cover was stored.
    /// </summary>
    public required OnlineCoverOutcomeDto Cover { get; init; }
}
