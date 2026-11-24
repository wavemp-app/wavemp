namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// What the planner of an organize operation needs to know about the media folders. Paths are relative to the folder
/// of the source with <c>/</c>. The real implementation reads the disk; tests describe a folder tree.
/// </summary>
public interface IOrganizeDisk
{
    /// <summary>
    /// The entries of a folder, or <c>null</c> if the folder does not exist (or is not safe to look into).
    /// </summary>
    IReadOnlyList<DiskEntry>? List(Guid sourceId, string directoryPath);

    /// <summary>
    /// Size and change time of a file, or <c>null</c> if it does not exist.
    /// </summary>
    FileFingerprint? Fingerprint(Guid sourceId, string relativePath);

    /// <summary>
    /// Whether the path stays inside the folder of the source and passes no link (see
    /// <see cref="Files.MediaPathGuard"/>).
    /// </summary>
    bool IsSafe(Guid sourceId, string relativePath, bool isTarget);

    /// <summary>
    /// Whether the file can be written (for its tags).
    /// </summary>
    bool IsWritable(Guid sourceId, string relativePath);

    /// <summary>
    /// Whether the file has the same content as the file at <paramref name="otherFullPath"/>.
    /// </summary>
    bool HasSameContent(Guid sourceId, string relativePath, string otherFullPath);
}

/// <summary>
/// An entry of a folder.
/// </summary>
/// <param name="Name">The name as on disk.</param>
/// <param name="IsDirectory">Whether it is a folder.</param>
public sealed record DiskEntry(string Name, bool IsDirectory);

/// <summary>
/// Size and change time of a file; the change time is truncated to microseconds, the precision of the library.
/// </summary>
public sealed record FileFingerprint(long Size, DateTimeOffset ModifiedAt)
{
    public static DateTimeOffset Truncate(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
    }

    /// <summary>
    /// Whether this is the file the library read (same size and change time).
    /// </summary>
    public bool Matches(long size, DateTimeOffset modifiedAt)
    {
        return Size == size && Truncate(ModifiedAt) == Truncate(modifiedAt);
    }
}
