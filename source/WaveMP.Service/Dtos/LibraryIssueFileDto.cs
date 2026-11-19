namespace WaveMP.Service.Dtos;

/// <summary>
/// The file of a problem of the library.
/// </summary>
public sealed class LibraryIssueFileDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The media source the file lies in.
    /// </summary>
    public required Guid SourceId { get; init; }

    public required string SourceName { get; init; }

    /// <summary>
    /// Path of the file relative to the folder of its media source with / as separator.
    /// </summary>
    public required string RelativePath { get; init; }

    public required MediaFileKindDto Kind { get; init; }
}
