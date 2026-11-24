namespace WaveMP.Data.Entities;

/// <summary>
/// One change of an organize operation: the files of one song move together (all keep the same base name) and its
/// tags may change, or the cover of an own album is written into its album folder.
/// </summary>
public class OrganizeItem
{
    public Guid Id { get; set; }

    public required Guid OperationId { get; set; }

    public OrganizeOperation? Operation { get; set; }

    /// <summary>
    /// Order of the item in the run, starting at 0.
    /// </summary>
    public required int Sequence { get; set; }

    public required OrganizeItemKind Kind { get; set; }

    public required OrganizeItemStatus Status { get; set; }

    /// <summary>
    /// The song; <c>null</c> for a cover item and after the song left the library.
    /// </summary>
    public Guid? TrackId { get; set; }

    public Track? Track { get; set; }

    /// <summary>
    /// The item of the undone operation this undo item puts back.
    /// </summary>
    public Guid? UndoOfItemId { get; set; }

    public required Guid SourceId { get; set; }

    /// <summary>
    /// Folder of the song before, relative to the folder of the source.
    /// </summary>
    public required string FromDirectoryPath { get; set; }

    /// <summary>
    /// Base name of the files of the song before.
    /// </summary>
    public required string FromBaseName { get; set; }

    /// <summary>
    /// Folder of the song afterwards, relative to the folder of the source.
    /// </summary>
    public required string ToDirectoryPath { get; set; }

    /// <summary>
    /// Base name of the files of the song afterwards.
    /// </summary>
    public required string ToBaseName { get; set; }

    /// <summary>
    /// Why the item cannot run; <c>null</c> without conflict.
    /// </summary>
    public OrganizeConflict? Conflict { get; set; }

    public List<OrganizeWarning> Warnings { get; set; } = [];

    /// <summary>
    /// Title of the song (or of the own album for a cover) at the time of the preview, for the log.
    /// </summary>
    public required string DisplayTitle { get; set; }

    public string? DisplayArtist { get; set; }

    /// <summary>
    /// The tags written into the primary audio file; <c>null</c> if no tag changes.
    /// </summary>
    public OrganizeTagValues? TagChanges { get; set; }

    /// <summary>
    /// The values of the changed tags at the time of the preview, as the library read them.
    /// </summary>
    public OrganizeTagValues? TagsBefore { get; set; }

    /// <summary>
    /// The backup of the tags written right before the tags were changed.
    /// </summary>
    public Guid? TagBackupId { get; set; }

    public TagBackup? TagBackup { get; set; }

    /// <summary>
    /// Folders the run created, relative to the folder of the source, outermost first.
    /// </summary>
    public List<string> CreatedFolders { get; set; } = [];

    /// <summary>
    /// Empty folders the run removed afterwards, relative to the folder of the source.
    /// </summary>
    public List<string> RemovedFolders { get; set; } = [];

    public DateTimeOffset? FinishedAt { get; set; }

    public ICollection<OrganizeFileMove> Moves { get; set; } = [];
}
