using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Metadata.Tests.Files;

[TestFixture]
public class MediaFileLocatorTests
{
    private static readonly Guid _musicId = Guid.NewGuid();
    private static readonly Guid _videosId = Guid.NewGuid();

    private TemporaryDirectory _root = null!;
    private MediaFileLocator _locator = null!;

    [SetUp]
    public void SetUp()
    {
        _root = new TemporaryDirectory();
        string media = _root.CreateDirectory("media");
        string videos = _root.CreateDirectory("videos");
        IMediaSourceCatalog catalog = Substitute.For<IMediaSourceCatalog>();
        MediaSourceInfo[] sources =
        [
            new(_musicId, "Music", media + Path.DirectorySeparatorChar, true, true),
            new(_videosId, "Videos", videos, false, false),
        ];
        catalog.Find(Arg.Any<Guid>()).Returns(call => Array.Find(sources, source => source.Id == call.Arg<Guid>()));
        _locator = new MediaFileLocator(catalog);
    }

    [TearDown]
    public void TearDown()
    {
        _root.Dispose();
    }

    [TestCase("Song.mp3", "Song.mp3")]
    [TestCase("Band/Album/01 - One.flac", "Band/Album/01 - One.flac")]
    [TestCase("Band/./Album/cover.jpg", "Band/Album/cover.jpg")]
    [TestCase("Band/Other/../Album/cover.jpg", "Band/Album/cover.jpg")]
    public void TryGetFullPath_PathInsideTheMediaFolder_ReturnsTheAbsolutePath(string relativePath, string expected)
    {
        bool found = _locator.TryGetFullPath(_musicId, relativePath, out string fullPath);

        found.Should().BeTrue();
        fullPath.Should().Be(_root.Combine("media/" + expected));
    }

    [Test]
    public void TryGetFullPath_OtherSource_ResolvesAgainstItsFolder()
    {
        // Also for a deactivated source: its tracks stay in the library and can be played.
        bool found = _locator.TryGetFullPath(_videosId, "Clip.mkv", out string fullPath);

        found.Should().BeTrue();
        fullPath.Should().Be(_root.Combine("videos/Clip.mkv"));
    }

    [Test]
    public void TryGetFullPath_UnknownSource_ReturnsFalse()
    {
        bool found = _locator.TryGetFullPath(Guid.NewGuid(), "Song.mp3", out string fullPath);

        found.Should().BeFalse();
        fullPath.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("..")]
    [TestCase("../secret.txt")]
    [TestCase("Band/../../secret.txt")]
    [TestCase("../media-other/Song.mp3")]
    [TestCase("../videos/Clip.mkv")]
    [TestCase(".")]
    [TestCase("Song\0.mp3")]
    public void TryGetFullPath_PathOutsideTheMediaFolder_ReturnsFalse(string relativePath)
    {
        bool found = _locator.TryGetFullPath(_musicId, relativePath, out string fullPath);

        found.Should().BeFalse();
        fullPath.Should().BeEmpty();
    }

    [Test]
    public void TryGetFullPath_RootedPath_ReturnsFalse()
    {
        string rooted = _root.Combine("media/Song.mp3");

        bool found = _locator.TryGetFullPath(_musicId, rooted, out _);

        found.Should().BeFalse();
        _locator.TryGetFullPath(_musicId, "/etc/passwd", out _).Should().BeFalse();
    }

    [Test]
    public void TryGetFullPath_FolderWithTheSamePrefix_ReturnsFalse()
    {
        // "media-other" starts with the name of the media folder but lies next to it.
        _root.CreateDirectory("media-other");

        bool found = _locator.TryGetFullPath(_musicId, "../media-other/Song.mp3", out _);

        found.Should().BeFalse();
    }
}
