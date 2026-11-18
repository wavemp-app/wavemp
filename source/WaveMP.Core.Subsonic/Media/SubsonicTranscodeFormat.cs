namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// The formats <c>stream</c> transcodes to.
/// </summary>
public enum SubsonicTranscodeFormat
{
    /// <summary>
    /// MP3 (LAME); the format every Subsonic app plays.
    /// </summary>
    Mp3,

    /// <summary>
    /// Opus in Ogg.
    /// </summary>
    Opus,

    /// <summary>
    /// AAC in ADTS.
    /// </summary>
    Aac,
}
