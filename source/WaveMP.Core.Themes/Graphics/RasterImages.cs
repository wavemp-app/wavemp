namespace WaveMP.Core.Themes.Graphics;

/// <summary>
/// Recognizes PNG and WebP pictures by their signature, never by the file extension alone.
/// </summary>
internal static class RasterImages
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(ReadOnlySpan<byte> content)
    {
        return content.StartsWith(PngSignature);
    }

    public static bool IsWebP(ReadOnlySpan<byte> content)
    {
        return content.Length >= 12 && content.StartsWith("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8);
    }
}
