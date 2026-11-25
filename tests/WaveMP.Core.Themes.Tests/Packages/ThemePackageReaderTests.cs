using System.IO.Compression;
using AwesomeAssertions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Packages;
using WaveMP.Core.Themes.Validations;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Packages;

/// <summary>
/// The structure of a theme package: limits against ZIP bombs, paths that could leave the package (zip slip) or mean
/// something else on another system, and the allowed file types.
/// </summary>
[TestFixture]
public class ThemePackageReaderTests
{
    private static ThemePackage Read(byte[] zip, ThemeOptions? options = null)
    {
        using MemoryStream stream = new(zip);
        return ThemePackageReader.Read(stream, options ?? new ThemeOptions());
    }

    private static ThemePackage Read(ThemePackageBuilder builder, ThemeOptions? options = null)
    {
        return Read(builder.Build(), options);
    }

    [Test]
    public void Read_ValidPackage_ReturnsItsFiles()
    {
        ThemePackage package = Read(new ThemePackageBuilder());

        package.Errors.Should().BeEmpty();
        package.Files.Keys.Should().BeEquivalentTo(
            "manifest.json",
            "theme.css",
            "images/glass.svg",
            "preview-light.png",
            "preview-dark.png");
    }

    [Test]
    public void Read_PackageWithAFont_ReturnsTheFont()
    {
        ThemePackage package = Read(new ThemePackageBuilder().WithFont());

        package.Errors.Should().BeEmpty();
        package.Files.Should().ContainKey("fonts/aurora.woff2");
    }

    [Test]
    public void Read_FilesInOneFolder_ReadsTheFolderAsRoot()
    {
        ThemePackage package = Read(new ThemePackageBuilder().InFolder("aurora"));

        package.Errors.Should().BeEmpty();
        package.Files.Keys.Should().Contain("manifest.json").And.Contain("images/glass.svg");
    }

    [Test]
    public void Read_SystemFilesAndDocuments_AreIgnored()
    {
        ThemePackage package = Read(new ThemePackageBuilder()
            .With("__MACOSX/._theme.css", "resource fork")
            .With(".DS_Store", "finder")
            .With("images/Thumbs.db", "thumbnails")
            .With("LICENSE", "MIT")
            .With("README.md", "# Aurora")
            .With("docs/notes.txt", "notes"));

        package.Errors.Should().BeEmpty();
        package.Files.Keys.Should().HaveCount(5);
    }

    [TestCase("../evil.svg")]
    [TestCase("images/../../evil.svg")]
    [TestCase("/etc/evil.svg")]
    [TestCase("C:/evil.svg")]
    [TestCase("images\\evil.svg")]
    [TestCase("..\\evil.svg")]
    [TestCase("images//evil.svg")]
    [TestCase("./evil.svg")]
    [TestCase(".hidden.svg")]
    [TestCase("images/ev il.svg")]
    [TestCase("images/%2e%2e/evil.svg")]
    [TestCase("images/évil.svg")]
    [TestCase("images/evil.svg\u0000.png")]
    [TestCase("a/b/c/d/e/evil.svg")]
    public void Read_PathThatCouldLeaveThePackage_IsRejected(string path)
    {
        ThemePackage package = Read(new ThemePackageBuilder().With(path, ThemePackageBuilder.GlassSvg));

        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.InvalidPath);
        package.Files.Keys.Should().NotContain(key => key.Contains("evil", StringComparison.Ordinal));
    }

    [Test]
    public void Read_VeryLongPath_IsRejected()
    {
        string path = string.Join('/', Enumerable.Repeat(new string('a', 60), 4)) + ".svg";

        ThemePackage package = Read(new ThemePackageBuilder().With(path, ThemePackageBuilder.GlassSvg));

        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.InvalidPath);
    }

    [TestCase("images/Glass.svg")]
    [TestCase("images/glass.svg")]
    public void Read_SamePathTwice_IsRejected(string second)
    {
        ThemePackage package = Read(new ThemePackageBuilder().WithDuplicate(second, ThemePackageBuilder.GlassSvg));

        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.DuplicatePath);
    }

    [TestCase("script.js")]
    [TestCase("images/page.html")]
    [TestCase("nested.zip")]
    [TestCase("fonts/own.woff")]
    [TestCase("fonts/own.ttf")]
    [TestCase("fonts/own.otf")]
    [TestCase("images/photo.jpg")]
    [TestCase("data.json")]
    [TestCase("theme.css.exe")]
    public void Read_UnsupportedFileType_IsRejected(string path)
    {
        ThemePackage package = Read(new ThemePackageBuilder().With(path, "x"));

        package.Errors.Should().ContainSingle().Which.Should().Be(new ThemeError(ThemeErrorCode.UnsupportedFile, path));
    }

    [Test]
    public void Read_TooManyEntries_IsRejectedBeforeUnpacking()
    {
        ThemePackageBuilder builder = new();
        for (int index = 0; index < 10; index++)
        {
            builder.With($"images/{index}.svg", ThemePackageBuilder.GlassSvg);
        }

        ThemePackage package = Read(builder, new ThemeOptions { MaxEntries = 10 });

        package.Errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(ThemeErrorCode.PackageTooManyEntries, Max: 10));
        package.Files.Should().BeEmpty();
    }

    [Test]
    public void Read_FileLargerThanTheLimit_IsRejected()
    {
        ThemePackage package = Read(
            new ThemePackageBuilder().With("images/big.png", [.. ThemePackageBuilder.Png, .. new byte[2048]]),
            new ThemeOptions { MaxFileBytes = 1024 });

        package.Errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(ThemeErrorCode.FileTooLarge, "images/big.png", Max: 1024));
    }

    [Test]
    public void Read_FontLargerThanItsLimit_IsRejected()
    {
        ThemePackage package = Read(
            new ThemePackageBuilder().With("fonts/big.woff2", ThemePackageBuilder.Woff2(payload: 2048)),
            new ThemeOptions { MaxFontBytes = 1024 });

        package.Errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(ThemeErrorCode.FileTooLarge, "fonts/big.woff2", Max: 1024));
    }

    [Test]
    public void Read_FontWithinItsLimit_MayBeLargerThanOtherFiles()
    {
        ThemePackage package = Read(
            new ThemePackageBuilder().With("fonts/big.woff2", ThemePackageBuilder.Woff2(payload: 2048)),
            new ThemeOptions { MaxFileBytes = 1024, MaxFontBytes = 4096 });

        package.Errors.Should().BeEmpty();
    }

    [Test]
    public void Read_StylesheetLargerThanItsLimit_IsRejected()
    {
        ThemePackage package = Read(
            new ThemePackageBuilder().With("theme.css", new string(' ', 5000)),
            new ThemeOptions { MaxStylesheetBytes = 4096 });

        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.FileTooLarge);
    }

    [Test]
    public void Read_ZipBomb_StopsAtTheLimitOfAllFiles()
    {
        // 40 files of 1 MiB of zeros each compress to a few kilobytes.
        ThemePackageBuilder builder = new();
        for (int index = 0; index < 40; index++)
        {
            builder.With($"images/{index}.png", [.. ThemePackageBuilder.Png, .. new byte[1024 * 1024]]);
        }

        byte[] zip = builder.Build();

        ThemePackage package = Read(zip, new ThemeOptions { MaxUnpackedBytes = 8 * 1024 * 1024 });

        zip.Length.Should().BeLessThan(1024 * 1024);
        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.PackageTooLarge);
        package.Files.Values.Sum(file => (long)file.Length).Should().BeLessThanOrEqualTo(8 * 1024 * 1024);
    }

    [Test]
    public void Read_EntryThatClaimsASmallerSize_IsMeasuredWhileUnpacking()
    {
        byte[] zip = new ThemePackageBuilder()
            .With("images/big.png", [.. ThemePackageBuilder.Png, .. new byte[4096]])
            .Build();
        PatchUncompressedSize(zip, "images/big.png", 16);

        ThemePackage package = Read(zip, new ThemeOptions { MaxFileBytes = 1024 });

        // Depending on the ZIP library, the entry is rejected as too large or damaged, or cut at the claimed size;
        // never are more bytes than the limit unpacked.
        (package.Files.GetValueOrDefault("images/big.png")?.Length ?? 0).Should().BeLessThanOrEqualTo(1024);
    }

    [Test]
    public void Read_DamagedZip_IsRejected()
    {
        byte[] zip = new ThemePackageBuilder().Build();

        ThemePackage package = Read(zip[..(zip.Length / 2)]);

        package.Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.PackageCorrupt);
    }

    [TestCase(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, true)]
    [TestCase(new byte[] { 0x50, 0x4B, 0x05, 0x06 }, true)]
    [TestCase(new byte[] { 0x1F, 0x8B, 0x08, 0x00 }, false)]
    [TestCase(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, false)]
    public void LooksLikeZip_RecognizesTheSignature(byte[] header, bool expected)
    {
        ThemePackageReader.LooksLikeZip(header).Should().Be(expected);
    }

    /// <summary>
    /// Writes a wrong uncompressed size into the local header and the central directory entry of a file, as a ZIP
    /// bomb would.
    /// </summary>
    private static void PatchUncompressedSize(byte[] zip, string name, uint size)
    {
        using (MemoryStream stream = new(zip))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
        {
            archive.GetEntry(name).Should().NotBeNull();
        }

        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        for (int index = 0; index + 46 < zip.Length; index++)
        {
            bool local = zip[index] == 0x50 && zip[index + 1] == 0x4B && zip[index + 2] == 3 && zip[index + 3] == 4;
            bool central = zip[index] == 0x50 && zip[index + 1] == 0x4B && zip[index + 2] == 1 && zip[index + 3] == 2;
            int nameOffset = local ? 30 : central ? 46 : -1;
            int sizeOffset = local ? 22 : central ? 24 : -1;
            if (nameOffset < 0 || index + nameOffset + nameBytes.Length > zip.Length ||
                !zip.AsSpan(index + nameOffset, nameBytes.Length).SequenceEqual(nameBytes))
            {
                continue;
            }

            BitConverter.TryWriteBytes(zip.AsSpan(index + sizeOffset, 4), size);
        }
    }
}
