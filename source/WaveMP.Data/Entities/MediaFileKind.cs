namespace WaveMP.Data.Entities;

/// <summary>
/// Category of a media file, decided by its extension (see the library settings; lyrics files always have the
/// extension <c>lrc</c>). Stored as its name.
/// </summary>
public enum MediaFileKind
{
    Audio,

    Video,

    /// <summary>
    /// A picture: the cover of a track, the cover of an album or a picture without a track.
    /// </summary>
    Image,

    /// <summary>
    /// A lyrics file (<c>.lrc</c>) with the base name of a track, e.g. <c>Song.lrc</c> next to <c>Song.mp3</c>.
    /// </summary>
    Lyrics,
}
