using System.Buffers.Binary;
using AwesomeAssertions;
using WaveMP.Core.Themes.Fonts;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Fonts;

[TestFixture]
public class WebFontsTests
{
    private static byte[] Font(Action<byte[]> change)
    {
        byte[] font = ThemePackageBuilder.Woff2();
        change(font);
        return font;
    }

    [Test]
    public void IsWoff2_ValidHeader_IsAccepted()
    {
        WebFonts.IsWoff2(ThemePackageBuilder.Woff2()).Should().BeTrue();
    }

    [TestCase(0x4F54544Fu)]
    [TestCase(0x74746366u)]
    public void IsWoff2_OtherFlavors_AreAccepted(uint flavor)
    {
        WebFonts.IsWoff2(Font(font => BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(4), flavor)))
            .Should().BeTrue();
    }

    [Test]
    public void IsWoff2_MetadataInsideTheFile_IsAccepted()
    {
        byte[] font = Font(font =>
        {
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(20), 8);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(28), 56);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(32), 8);
        });

        WebFonts.IsWoff2(font).Should().BeTrue();
    }

    [Test]
    public void IsWoff2_ShorterThanTheHeader_IsRejected()
    {
        WebFonts.IsWoff2(ThemePackageBuilder.Woff2()[..47]).Should().BeFalse();
    }

    [Test]
    public void IsWoff2_OtherFormats_AreRejected()
    {
        // WOFF 1, TrueType and a PNG picture renamed to .woff2.
        WebFonts.IsWoff2(Font(font => "wOFF"u8.CopyTo(font))).Should().BeFalse();
        WebFonts.IsWoff2(Font(font => BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000))).Should().BeFalse();
        WebFonts.IsWoff2([.. ThemePackageBuilder.Png, .. new byte[64]]).Should().BeFalse();
    }

    [Test]
    public void IsWoff2_UnknownFlavor_IsRejected()
    {
        WebFonts.IsWoff2(Font(font => "<htm"u8.CopyTo(font.AsSpan(4)))).Should().BeFalse();
    }

    [Test]
    public void IsWoff2_LengthOtherThanTheFile_IsRejected()
    {
        byte[] font = ThemePackageBuilder.Woff2();

        WebFonts.IsWoff2([.. font, 0]).Should().BeFalse();
        WebFonts.IsWoff2(font[..^1]).Should().BeFalse();
    }

    [Test]
    public void IsWoff2_NoTables_IsRejected()
    {
        WebFonts.IsWoff2(Font(font => BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(12), 0))).Should().BeFalse();
    }

    [Test]
    public void IsWoff2_ReservedFieldSet_IsRejected()
    {
        WebFonts.IsWoff2(Font(font => BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(14), 1))).Should().BeFalse();
    }

    [TestCase(0u)]
    [TestCase(17u)]
    public void IsWoff2_CompressedDataOfAnotherLength_IsRejected(uint length)
    {
        WebFonts.IsWoff2(Font(font => BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(20), length)))
            .Should().BeFalse();
    }

    [TestCase(28, 0u, 8u)]
    [TestCase(28, 60u, 8u)]
    [TestCase(28, 0xFFFFFFF0u, 0x20u)]
    [TestCase(40, 20u, 4u)]
    [TestCase(40, 60u, 8u)]
    public void IsWoff2_BlockOutsideTheFile_IsRejected(int field, uint offset, uint length)
    {
        byte[] font = Font(font =>
        {
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(field), offset);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(field + 4), length);
        });

        WebFonts.IsWoff2(font).Should().BeFalse();
    }
}
