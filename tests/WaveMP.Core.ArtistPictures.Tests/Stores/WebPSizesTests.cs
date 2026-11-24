using System.Text;
using AwesomeAssertions;
using WaveMP.Core.ArtistPictures.Stores;

namespace WaveMP.Core.ArtistPictures.Tests.Stores;

[TestFixture]
public class WebPSizesTests
{
    [Test]
    public void TryRead_LossyHeader_ReadsTheSize()
    {
        byte[] header = Header("VP8 ", [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A, 0xE8, 0x03, 0x2C, 0x01]);

        WebPSizes.TryRead(header, out int width, out int height).Should().BeTrue();

        (width, height).Should().Be((1000, 300));
    }

    [Test]
    public void TryRead_LosslessHeader_ReadsTheSize()
    {
        // Width - 1 = 95 in bits 0 to 13, height - 1 = 95 in bits 14 to 27.
        uint bits = 95u | (95u << 14);
        byte[] header = Header("VP8L", [0x2F, (byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24)]);

        WebPSizes.TryRead(header, out int width, out int height).Should().BeTrue();

        (width, height).Should().Be((96, 96));
    }

    [Test]
    public void TryRead_ExtendedHeader_ReadsTheCanvasSize()
    {
        byte[] header = Header("VP8X", [0x10, 0, 0, 0, 0xE7, 0x03, 0x00, 0x2B, 0x01, 0x00]);

        WebPSizes.TryRead(header, out int width, out int height).Should().BeTrue();

        (width, height).Should().Be((1000, 300));
    }

    [Test]
    public void TryRead_OtherContent_ReturnsFalse()
    {
        WebPSizes.TryRead("not a picture at all, just some text"u8, out _, out _).Should().BeFalse();
        WebPSizes.TryRead(Header("VP8 ", [0x30, 0x01, 0x00, 0x00, 0x00, 0x00]), out _, out _).Should().BeFalse();
        WebPSizes.TryRead(Header("ALPH", []), out _, out _).Should().BeFalse();
        WebPSizes.TryRead(Header("VP8 ", [])[..20], out _, out _).Should().BeFalse();
    }

    private static byte[] Header(string chunk, byte[] data)
    {
        byte[] header = new byte[WebPSizes.HeaderLength];
        "RIFF"u8.CopyTo(header);
        "WEBP"u8.CopyTo(header.AsSpan(8));
        Encoding.ASCII.GetBytes(chunk).CopyTo(header, 12);
        data.CopyTo(header, 20);
        return header;
    }
}
