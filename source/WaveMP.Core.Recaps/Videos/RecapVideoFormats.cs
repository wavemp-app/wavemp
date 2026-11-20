namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// Recognizes the containers of recorded videos by their first bytes.
/// </summary>
public static class RecapVideoFormats
{
    /// <summary>
    /// Number of bytes <see cref="IsSupported"/> looks at.
    /// </summary>
    public const int HeaderLength = 12;

    /// <summary>
    /// Whether the bytes start a WebM or Matroska file (EBML header) or an MP4 file (box <c>ftyp</c>).
    /// </summary>
    public static bool IsSupported(ReadOnlySpan<byte> header)
    {
        bool ebml = header.Length >= 4
            && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
        bool mp4 = header.Length >= 8
            && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p';
        return ebml || mp4;
    }
}
