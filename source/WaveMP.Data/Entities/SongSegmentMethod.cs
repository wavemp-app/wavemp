namespace WaveMP.Data.Entities;

/// <summary>
/// How the song analysis found the song segment of a media file, from the most to the least reliable. Stored as its
/// name.
/// </summary>
public enum SongSegmentMethod
{
    /// <summary>
    /// Nothing points to an intro or outro: the whole file is the song.
    /// </summary>
    WholeFile,

    /// <summary>
    /// Aligned with the other recording of the track (the audio file next to the video).
    /// </summary>
    Alignment,

    /// <summary>
    /// Aligned with the synchronized lyrics: where the sung lines fit the timestamps of the lyrics best.
    /// </summary>
    Lyrics,

    /// <summary>
    /// From the structure of the file: silence between speech or noise at the start or end and the music.
    /// </summary>
    Structure,
}
