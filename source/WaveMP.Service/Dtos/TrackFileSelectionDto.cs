namespace WaveMP.Service.Dtos;

/// <summary>
/// The file selection of a track: per category the file it uses, the selection of the admin and the files to choose
/// from.
/// </summary>
public sealed class TrackFileSelectionDto
{
    public required Guid TrackId { get; init; }

    /// <summary>
    /// The audio file the track plays.
    /// </summary>
    public required FileSelectionDto Audio { get; init; }

    /// <summary>
    /// The video file the track plays.
    /// </summary>
    public required FileSelectionDto Video { get; init; }

    /// <summary>
    /// The cover of the track itself. A selected cover is shown for the track before the cover of its album.
    /// </summary>
    public required FileSelectionDto Cover { get; init; }
}
