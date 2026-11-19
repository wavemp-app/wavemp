using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// A track with all its files, its own covers and its file selections, not tracked.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="SourceId">Id of the media source of the track.</param>
/// <param name="DirectoryPath">Folder of the track relative to the folder of its source.</param>
/// <param name="Files">All files of the track: audio, video and pictures.</param>
/// <param name="Covers">The covers of the track itself (not of its album).</param>
/// <param name="Selections">The selections of the admin, at most one per category.</param>
public sealed record TrackFiles(
    Guid TrackId,
    Guid SourceId,
    string DirectoryPath,
    IReadOnlyList<MediaFile> Files,
    IReadOnlyList<Cover> Covers,
    IReadOnlyList<TrackFileSelection> Selections);
