using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The file whose sound a track plays (its primary audio file, otherwise its primary video), as the last scan found it.
/// </summary>
/// <param name="RelativePath">Path relative to the media folder with <c>/</c> as separator.</param>
/// <param name="Extension">Extension in lower case without the dot.</param>
/// <param name="Kind">Audio file or video.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Bitrate">Bitrate in bit/s.</param>
/// <param name="SampleRate">Sample rate in Hz.</param>
/// <param name="BitDepth">Bits per sample.</param>
/// <param name="Channels">Number of channels.</param>
public sealed record SubsonicFileRow(
    string RelativePath,
    string Extension,
    MediaFileKind Kind,
    long Size,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth,
    int? Channels);
