namespace WaveMP.Core.OnlineMetadata.Covers;

/// <summary>
/// Stores a cover from the online search for the own library. The cover of an album always goes into the data folder,
/// named after the id of the album, so it never becomes the cover of another album in the same folder. The cover of a
/// song goes as picture with its name next to it into the media folder, if the folder can be written, otherwise into
/// the data folder.
/// </summary>
public interface IOnlineCoverWriter
{
    /// <summary>
    /// Stores the picture for the target. For a song in the media folder, pictures with the same base name are replaced
    /// (a copy of each is kept in the folder <c>online-covers/replaced</c> of the data folder) and the folder is read
    /// again; an online cover of the same song in the data folder is removed. Otherwise the picture becomes the online
    /// cover of the album or song in the data folder, replacing an earlier one; the media folder is not touched.
    /// </summary>
    Task<CoverSaveOutcome> SaveAsync(
        CoverTarget target,
        byte[] picture,
        Guid releaseId,
        CancellationToken cancellationToken = default);
}
