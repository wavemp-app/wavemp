namespace WaveMP.Service.Dtos;

/// <summary>
/// The files the admin selects for a track. Replaces the whole selection: a missing or null id leaves the choice of
/// the category to the quality rule. Sending the id of the current selection keeps it, also a missing one.
/// </summary>
public sealed class UpdateTrackFileSelectionDto
{
    /// <summary>
    /// Id of an audio file of the track.
    /// </summary>
    public Guid? AudioFileId { get; init; }

    /// <summary>
    /// Id of a video file of the track.
    /// </summary>
    public Guid? VideoFileId { get; init; }

    /// <summary>
    /// Id of a readable picture file of the track, or of an audio or video file of the track with an embedded
    /// picture.
    /// </summary>
    public Guid? CoverFileId { get; init; }
}
