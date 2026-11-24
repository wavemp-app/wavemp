namespace WaveMP.Service.Dtos;

/// <summary>
/// One change of an organize operation: the files of a song move together (all keep the same base name) and its tags
/// may change, or a cover or the files of a folder without a song move. Paths are relative to the folder of the media
/// source, with / as separator.
/// </summary>
public sealed class OrganizeItemDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Order of the item in the run, starting at 0.
    /// </summary>
    public required int Sequence { get; init; }

    public required OrganizeItemKindDto Kind { get; init; }

    public required OrganizeItemStatusDto Status { get; init; }

    /// <summary>
    /// The song; null for other kinds and after the song left the library.
    /// </summary>
    public Guid? TrackId { get; init; }

    public required Guid SourceId { get; init; }

    /// <summary>
    /// Folder before; empty for the folder of the source.
    /// </summary>
    public required string FromDirectoryPath { get; init; }

    /// <summary>
    /// Base name of the files of the song before; empty for other kinds.
    /// </summary>
    public required string FromBaseName { get; init; }

    /// <summary>
    /// Folder afterwards; empty for the folder of the source.
    /// </summary>
    public required string ToDirectoryPath { get; init; }

    /// <summary>
    /// Base name of the files of the song afterwards; empty for other kinds.
    /// </summary>
    public required string ToBaseName { get; init; }

    /// <summary>
    /// Why the item cannot run; null without conflict.
    /// </summary>
    public OrganizeConflictDto? Conflict { get; init; }

    public required IReadOnlyList<OrganizeWarningDto> Warnings { get; init; }

    /// <summary>
    /// Title of the song (or of the album or folder) at the time of the preview.
    /// </summary>
    public required string DisplayTitle { get; init; }

    public string? DisplayArtist { get; init; }

    /// <summary>
    /// The tags written into the audio file; null if the tags stay.
    /// </summary>
    public OrganizeTagValuesDto? TagChanges { get; init; }

    /// <summary>
    /// The values of the changed tags before, as the library read them.
    /// </summary>
    public OrganizeTagValuesDto? TagsBefore { get; init; }

    /// <summary>
    /// The files that move, in their order.
    /// </summary>
    public required IReadOnlyList<OrganizeFileMoveDto> Moves { get; init; }
}
