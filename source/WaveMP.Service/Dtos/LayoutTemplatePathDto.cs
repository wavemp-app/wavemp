namespace WaveMP.Service.Dtos;

/// <summary>
/// Where a layout template puts one song, relative to the folder of its media source. Names are already cleaned of
/// characters that file names cannot hold.
/// </summary>
public sealed class LayoutTemplatePathDto
{
    /// <summary>
    /// The track of the library, or <c>null</c> for an example song.
    /// </summary>
    public required Guid? TrackId { get; init; }

    /// <summary>
    /// The folder names, outermost first; empty for a file-based template.
    /// </summary>
    public required IReadOnlyList<string> Folders { get; init; }

    /// <summary>
    /// The file name without extension; all files of the song get it.
    /// </summary>
    public required string BaseName { get; init; }

    /// <summary>
    /// Extension of the file the song plays, without dot, or <c>null</c>.
    /// </summary>
    public required string? Extension { get; init; }

    /// <summary>
    /// Whether the path is longer than the scan allows; such a song cannot be moved.
    /// </summary>
    public required bool IsTooLong { get; init; }
}
