namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The pictures of the online covers in the folder <c>online-covers</c> of the data folder, one file per cover named
/// after its id, e.g. <c>0193…a1.jpg</c>.
/// </summary>
public interface IOnlineCoverFiles
{
    /// <summary>
    /// The absolute path of a picture, or <c>null</c> for a name that is no name of an online cover (so a path never
    /// leaves the folder).
    /// </summary>
    string? GetFullPath(string fileName);

    /// <summary>
    /// The file name of the picture of a cover, by the format recognized from its content; <c>null</c> for formats
    /// that are no cover (see <see cref="ImageFormats"/>).
    /// </summary>
    string? FileNameOf(Guid coverId, ReadOnlySpan<byte> picture);

    /// <summary>
    /// Stores a picture under the name. Returns <c>false</c> if the folder cannot be written.
    /// </summary>
    Task<bool> SaveAsync(string fileName, ReadOnlyMemory<byte> picture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a picture; a missing one is fine.
    /// </summary>
    void Delete(string fileName);

    /// <summary>
    /// The names of all stored pictures.
    /// </summary>
    IReadOnlyList<string> ListFileNames();
}
