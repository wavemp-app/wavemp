using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A file of the library as the organization of the media files needs it.
/// </summary>
/// <param name="MediaFileId">Id of the file.</param>
/// <param name="RelativePath">Path relative to the folder of the source.</param>
/// <param name="FileName">Name of the file as on disk.</param>
/// <param name="Kind">Category of the file.</param>
/// <param name="Size">Size in bytes as the library read it.</param>
/// <param name="ModifiedAt">Change time as the library read it.</param>
/// <param name="IsPrimary">Whether the song plays this file.</param>
/// <param name="IsWritable">Whether the library found its tags writable.</param>
/// <param name="Tags">The tags as read; <c>null</c> for pictures and lyrics.</param>
public sealed record OrganizeFileRow(
    Guid MediaFileId,
    string RelativePath,
    string FileName,
    MediaFileKind Kind,
    long Size,
    DateTimeOffset ModifiedAt,
    bool IsPrimary,
    bool IsWritable,
    MediaFileTags? Tags);
