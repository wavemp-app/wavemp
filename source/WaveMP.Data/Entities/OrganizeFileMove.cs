namespace WaveMP.Data.Entities;

/// <summary>
/// One file of an organize item: where it is and where it goes. Paths are relative to the folder of the source. A
/// move without <see cref="FromPath"/> writes the cover of an own album from the data folder; a move without
/// <see cref="ToPath"/> removes a file the undone operation wrote (only while it is unchanged).
/// </summary>
public class OrganizeFileMove
{
    public Guid Id { get; set; }

    public required Guid ItemId { get; set; }

    public OrganizeItem? Item { get; set; }

    /// <summary>
    /// Order of the move in its item, starting at 0.
    /// </summary>
    public required int Sequence { get; set; }

    /// <summary>
    /// The file of the library; <c>null</c> for the cover of an own album and after the file left the library.
    /// </summary>
    public Guid? MediaFileId { get; set; }

    public MediaFile? MediaFile { get; set; }

    public required Guid SourceId { get; set; }

    public required OrganizeFileKind FileKind { get; set; }

    public string? FromPath { get; set; }

    public string? ToPath { get; set; }

    /// <summary>
    /// A hidden name in the target folder the file passes through when only the case or the Unicode normalization of
    /// its name changes, so the rename works on every file system.
    /// </summary>
    public string? TempPath { get; set; }

    /// <summary>
    /// Size of the file in bytes right before it is moved; with <see cref="ModifiedAt"/> the fingerprint that tells
    /// whether the file is still the one of the preview (the run updates both after writing tags).
    /// </summary>
    public required long Size { get; set; }

    /// <summary>
    /// Time of the last change of the file, truncated to microseconds.
    /// </summary>
    public required DateTimeOffset ModifiedAt { get; set; }

    public required OrganizeMoveStatus Status { get; set; }
}
