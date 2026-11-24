using System.Buffers.Binary;

namespace WaveMP.Core.ArtistPictures.Stores;

/// <summary>
/// Reads width and height from the header of a WebP picture (simple lossy, lossless and extended format), so the
/// size of a stored picture is known without another run of ffmpeg.
/// </summary>
internal static class WebPSizes
{
    /// <summary>
    /// Bytes of the header that hold the size in every format.
    /// </summary>
    public const int HeaderLength = 30;

    public static bool TryRead(ReadOnlySpan<byte> header, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (header.Length < HeaderLength || !header.StartsWith("RIFF"u8) || !header[8..12].SequenceEqual("WEBP"u8))
        {
            return false;
        }

        ReadOnlySpan<byte> chunk = header[12..16];
        ReadOnlySpan<byte> data = header[20..];
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Frame tag (3 bytes) and start code 9D 01 2A, then 14 bits of width and of height.
            if (data[3] != 0x9D || data[4] != 0x01 || data[5] != 0x2A)
            {
                return false;
            }

            width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]) & 0x3FFF;
        }
        else if (chunk.SequenceEqual("VP8L"u8))
        {
            // Signature 0x2F, then 14 bits of width - 1 and of height - 1.
            if (data[0] != 0x2F)
            {
                return false;
            }

            uint bits = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
        }
        else if (chunk.SequenceEqual("VP8X"u8))
        {
            // Flags (4 bytes), then 24 bits of the canvas width - 1 and of its height - 1.
            width = (data[4] | (data[5] << 8) | (data[6] << 16)) + 1;
            height = (data[7] | (data[8] << 8) | (data[9] << 16)) + 1;
        }
        else
        {
            return false;
        }

        return width > 0 && height > 0;
    }
}
