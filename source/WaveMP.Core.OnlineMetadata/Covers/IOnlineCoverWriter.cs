namespace WaveMP.Core.OnlineMetadata.Covers;

/// <summary>
/// Stores a cover from the online search for the own library: as picture in the media folder where the cover rules
/// expect it, if the folder can be written, otherwise in the data folder.
/// </summary>
public interface IOnlineCoverWriter
{
    /// <summary>
    /// Stores the picture at the target. In the media folder, pictures with the same base name are replaced (a copy of
    /// each is kept in the folder <c>online-covers/replaced</c> of the data folder) and the folder is read again; an
    /// online cover of the same album or song in the data folder is removed. Otherwise the picture becomes the online
    /// cover of the album or song, replacing an earlier one.
    /// </summary>
    Task<CoverSaveOutcome> SaveAsync(
        CoverTarget target,
        byte[] picture,
        Guid releaseId,
        CancellationToken cancellationToken = default);
}
