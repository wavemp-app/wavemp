using AwesomeAssertions;
using WaveMP.Core.Scanning.Sources;

namespace WaveMP.Core.Scanning.Tests.Sources;

[TestFixture]
public class MediaSourcePathsTests
{
    private static readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wavemp-sources"));

    [Test]
    public void TryNormalize_AbsolutePath_RemovesDotsAndTheSeparatorAtTheEnd()
    {
        string value = Path.Combine(_root, "music", ".", "other", "..") + Path.DirectorySeparatorChar;

        bool valid = MediaSourcePaths.TryNormalize("  " + value + "  ", out string path);

        valid.Should().BeTrue();
        path.Should().Be(Path.Combine(_root, "music"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("music")]
    [TestCase("./music")]
    [TestCase("../music")]
    public void TryNormalize_EmptyOrRelativePath_IsInvalid(string? value)
    {
        MediaSourcePaths.TryNormalize(value, out string path).Should().BeFalse();
        path.Should().BeEmpty();
    }

    [Test]
    public void TryNormalize_NullCharacter_IsInvalid()
    {
        MediaSourcePaths.TryNormalize(Path.Combine(_root, "mu\0sic"), out _).Should().BeFalse();
    }

    [TestCase("music", "music", true)]
    [TestCase("music", "music/rock", true)]
    [TestCase("music/rock", "music", true)]
    [TestCase("music", "music-videos", false)]
    [TestCase("music", "videos", false)]
    public void Overlap_TwoFolders_IsTrueForTheSameOrNestedFolders(string first, string second, bool expected)
    {
        MediaSourcePaths.Overlap(Combine(first), Combine(second)).Should().Be(expected);
        MediaSourcePaths.Overlap(Combine(second), Combine(first)).Should().Be(expected);
    }

    [Test]
    public void Overlap_SeparatorAtTheEnd_DoesNotCount()
    {
        string music = Combine("music");

        MediaSourcePaths.Overlap(music + Path.DirectorySeparatorChar, music).Should().BeTrue();
        MediaSourcePaths.Overlap(music + Path.DirectorySeparatorChar, Combine("music-videos")).Should().BeFalse();
    }

    private static string Combine(string relative)
    {
        return Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
