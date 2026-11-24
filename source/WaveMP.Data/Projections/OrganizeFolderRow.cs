namespace WaveMP.Data.Projections;

/// <summary>
/// A folder of the library: its songs and its files that belong to no song (e.g. the album cover).
/// </summary>
/// <param name="SourceId">The media source.</param>
/// <param name="DirectoryPath">The folder relative to the folder of the source.</param>
/// <param name="TrackIds">All songs of the folder.</param>
/// <param name="LooseFiles">Files of the folder without a song.</param>
public sealed record OrganizeFolderRow(
    Guid SourceId,
    string DirectoryPath,
    IReadOnlyList<Guid> TrackIds,
    IReadOnlyList<OrganizeFileRow> LooseFiles);
