using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// What a rescan compares of a stored media file.
/// </summary>
/// <param name="Id">Id of the file.</param>
/// <param name="RelativePath">Path relative to the folder of the source.</param>
/// <param name="DirectoryPath">Folder relative to the folder of the source.</param>
/// <param name="Kind">Category of the file.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="ModifiedAt">Time of the last change.</param>
/// <param name="MetadataVersion">Version of the metadata reader that read the file.</param>
/// <param name="TrackId">The track the file belongs to; <c>null</c> for pictures without a track.</param>
/// <param name="IsPrimary">Whether the track plays the file (see <see cref="MediaFile.IsPrimary"/>), so a scan can
/// recognize a song whose files were renamed or moved by it.</param>
public sealed record MediaFileSnapshot(
    Guid Id,
    string RelativePath,
    string DirectoryPath,
    MediaFileKind Kind,
    long Size,
    DateTimeOffset ModifiedAt,
    int MetadataVersion,
    Guid? TrackId = null,
    bool IsPrimary = false);
