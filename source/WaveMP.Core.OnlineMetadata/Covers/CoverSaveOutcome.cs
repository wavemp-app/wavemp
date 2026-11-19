namespace WaveMP.Core.OnlineMetadata.Covers;

/// <summary>
/// Where a cover from the online search was stored.
/// </summary>
public enum CoverSaveOutcome
{
    /// <summary>
    /// No cover was asked for.
    /// </summary>
    None,

    /// <summary>
    /// As picture in the media folder, named after the cover rules (e.g. <c>cover.jpg</c> in the folder of the album
    /// or <c>Song.jpg</c> next to the song).
    /// </summary>
    MediaFolder,

    /// <summary>
    /// In the data folder, because the media folder cannot be written; only WaveMP shows it.
    /// </summary>
    DataFolder,

    /// <summary>
    /// Neither the media folder nor the data folder could be written.
    /// </summary>
    NotSaved,
}
