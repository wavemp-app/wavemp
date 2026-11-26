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
    /// As picture with the name of the song next to it in the media folder (<c>Song.jpg</c>).
    /// </summary>
    MediaFolder,

    /// <summary>
    /// In the data folder: always for the cover of an album, for the cover of a song if the media folder cannot be
    /// written. Only WaveMP shows it.
    /// </summary>
    DataFolder,

    /// <summary>
    /// Neither the media folder nor the data folder could be written.
    /// </summary>
    NotSaved,
}
