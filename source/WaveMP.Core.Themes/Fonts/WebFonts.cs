using System.Buffers.Binary;

namespace WaveMP.Core.Themes.Fonts;

/// <summary>
/// Recognizes WOFF2 fonts by their header, never by the file extension alone. The check covers the signature, the
/// flavor of the font inside, the length the header names (it must be the length of the file) and the position of
/// the optional metadata and private blocks. The tables themselves are checked by the browser, which sanitizes every
/// web font before it uses it.
/// </summary>
internal static class WebFonts
{
    /// <summary>
    /// Length of the WOFF2 header.
    /// </summary>
    public const int Woff2HeaderLength = 48;

    // Flavors of the font in a WOFF2 file: TrueType outlines, CFF outlines, old Apple TrueType and font collections.
    private static readonly uint[] _flavors =
    [
        0x00010000,
        0x4F54544F, // OTTO
        0x74727565, // true
        0x74746366, // ttcf
    ];

    public static bool IsWoff2(ReadOnlySpan<byte> content)
    {
        if (content.Length < Woff2HeaderLength || !content.StartsWith("wOF2"u8))
        {
            return false;
        }

        uint flavor = BinaryPrimitives.ReadUInt32BigEndian(content[4..]);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(content[8..]);
        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(content[12..]);
        ushort reserved = BinaryPrimitives.ReadUInt16BigEndian(content[14..]);
        uint compressedLength = BinaryPrimitives.ReadUInt32BigEndian(content[20..]);
        uint metadataOffset = BinaryPrimitives.ReadUInt32BigEndian(content[28..]);
        uint metadataLength = BinaryPrimitives.ReadUInt32BigEndian(content[32..]);
        uint privateOffset = BinaryPrimitives.ReadUInt32BigEndian(content[40..]);
        uint privateLength = BinaryPrimitives.ReadUInt32BigEndian(content[44..]);

        return Array.IndexOf(_flavors, flavor) >= 0 &&
            length == content.Length &&
            tableCount > 0 &&
            reserved == 0 &&
            compressedLength > 0 &&
            compressedLength <= length - Woff2HeaderLength &&
            IsWithin(metadataOffset, metadataLength, length) &&
            IsWithin(privateOffset, privateLength, length);
    }

    /// <summary>
    /// Whether an optional block lies after the header and inside the file; an empty block may have any offset.
    /// </summary>
    private static bool IsWithin(uint offset, uint length, uint fileLength)
    {
        return length == 0 || (offset >= Woff2HeaderLength && (ulong)offset + length <= fileLength);
    }
}
