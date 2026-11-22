using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Writers;

/// <summary>
/// Versions of what the scan stores, per kind of file. Raising the version of a kind makes the next scan read every
/// file of that kind again, e.g. when a later version stores more of a file.
/// </summary>
public static class ScannerVersions
{
    /// <summary>
    /// Version of the metadata of audio files. Version 2 adds the compilation flag of the tags, which marks albums as
    /// compilations.
    /// </summary>
    public const int AudioMetadata = 2;

    /// <summary>
    /// Version of the metadata of videos. Version 2 adds the codec of the audio stream and the pixel format, which the
    /// decision between direct play and transcoding needs.
    /// </summary>
    public const int VideoMetadata = 2;

    /// <summary>
    /// Version of the metadata of pictures.
    /// </summary>
    public const int ImageMetadata = 1;

    /// <summary>
    /// Version of lyrics files; the scan only stores their size and change time.
    /// </summary>
    public const int LyricsMetadata = 1;

    /// <summary>
    /// Version of the rules that form albums. Version 2 identifies an album by its artist (the album artist, otherwise
    /// the artist of the track) and title, wherever its tracks lie, instead of per folder with compilations of several
    /// artists. Version 3 identifies the album of tracks with the compilation flag and without album artist tag by its
    /// title and album folder in the media source, whatever the artists of the tracks are. Raising it makes the next
    /// scan form the albums of all stored folders again, without reading files (see
    /// <see cref="MediaSource.AlbumGroupingVersion"/>).
    /// </summary>
    public const int AlbumGrouping = 3;

    /// <summary>
    /// The current version of the metadata of a kind of file.
    /// </summary>
    public static int MetadataOf(MediaFileKind kind)
    {
        return kind switch
        {
            MediaFileKind.Video => VideoMetadata,
            MediaFileKind.Image => ImageMetadata,
            MediaFileKind.Lyrics => LyricsMetadata,
            _ => AudioMetadata,
        };
    }
}
