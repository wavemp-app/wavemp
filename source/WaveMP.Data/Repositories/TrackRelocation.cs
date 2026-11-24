namespace WaveMP.Data.Repositories;

/// <summary>
/// The new place of the files of a done organize item, written in one transaction with the state of the item.
/// </summary>
/// <param name="OperationId">The operation.</param>
/// <param name="ItemId">The item that is done.</param>
/// <param name="TrackId">The song whose folder and base name change; <c>null</c> for a cover item.</param>
/// <param name="DirectoryPath">New folder of the song relative to the folder of the source.</param>
/// <param name="BaseName">New base name of the song.</param>
/// <param name="Files">The moved files.</param>
/// <param name="CreatedFolders">Folders the item created.</param>
/// <param name="FinishedAt">When the item was done.</param>
public sealed record TrackRelocation(
    Guid OperationId,
    Guid ItemId,
    Guid? TrackId,
    string DirectoryPath,
    string BaseName,
    IReadOnlyList<FileRelocation> Files,
    IReadOnlyList<string> CreatedFolders,
    DateTimeOffset FinishedAt);

/// <summary>
/// A moved file of a <see cref="TrackRelocation"/>.
/// </summary>
/// <param name="MoveId">The move of the journal.</param>
/// <param name="MediaFileId">The file of the library; <c>null</c> for a file the library does not know yet.</param>
/// <param name="RelativePath">Its new path relative to the folder of the source; <c>null</c> if the file was
/// removed.</param>
public sealed record FileRelocation(Guid MoveId, Guid? MediaFileId, string? RelativePath);
