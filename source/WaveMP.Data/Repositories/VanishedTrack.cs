namespace WaveMP.Data.Repositories;

/// <summary>
/// A stored track whose files are all gone from their place, as a scan saw it: it is deleted at the end of the scan
/// unless the scan found the song at a new place and kept it there (see
/// <see cref="ILibraryScanRepository.DeleteVanishedTracksAsync"/>).
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="SourceId">Id of the media source the track and its files lay in.</param>
/// <param name="DirectoryPath">Folder of the track relative to the folder of its source.</param>
/// <param name="Files">The stored files of the track with the paths they had, relative to the folder of the
/// source.</param>
public sealed record VanishedTrack(
    Guid TrackId,
    Guid SourceId,
    string DirectoryPath,
    IReadOnlyList<(Guid FileId, string RelativePath)> Files);
