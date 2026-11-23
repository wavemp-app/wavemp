namespace WaveMP.Core.Scanning.Moves;

/// <summary>
/// A song a scan found at a new place: a new file that looks like the file a vanished track played (see
/// <see cref="TrackMovePlanner"/>). The track keeps its id and all its data once the new file was read and
/// <see cref="TrackMoveEvidence"/> confirms it.
/// </summary>
/// <param name="SourceId">Id of the media source of the new file.</param>
/// <param name="RelativePath">Path of the new file relative to the folder of its source.</param>
/// <param name="TrackId">Id of the vanished track.</param>
/// <param name="FileId">Id of the file the vanished track played (its primary audio file, otherwise its primary
/// video file); the new file takes it over.</param>
/// <param name="OldSourceId">Id of the media source the vanished track lay in.</param>
/// <param name="OldDirectoryPath">Folder the vanished track lay in, relative to the folder of its source.</param>
/// <param name="OldRelativePath">Path the file the vanished track played had, relative to the folder of its
/// source.</param>
public sealed record TrackMove(
    Guid SourceId,
    string RelativePath,
    Guid TrackId,
    Guid FileId,
    Guid OldSourceId,
    string OldDirectoryPath,
    string OldRelativePath);
