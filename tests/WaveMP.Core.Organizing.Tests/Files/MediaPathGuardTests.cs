using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

public sealed class MediaPathGuardTests
{
    private TemporaryDirectory _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new TemporaryDirectory();
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Dispose();
    }

    [TestCase("Artist/Album/01 Song.mp3")]
    [TestCase("Song.mp3")]
    [TestCase("Not/Yet/Created.flac")]
    public void A_path_inside_the_source_is_resolved(string relativePath)
    {
        MediaPathGuard.TryResolve(_directory.Path, relativePath, isTarget: true, out string fullPath).Should().BeTrue();

        fullPath.Should().Be(Path.Combine([_directory.Path, .. relativePath.Split('/')]));
    }

    [TestCase("")]
    [TestCase("../outside.mp3")]
    [TestCase("Artist/../../outside.mp3")]
    [TestCase("Artist/./Song.mp3")]
    [TestCase("Artist//Song.mp3")]
    [TestCase("/etc/passwd")]
    [TestCase("Artist\\..\\..\\Song.mp3")]
    [TestCase("Artist/Song\0.mp3")]
    public void A_path_that_could_leave_the_source_is_refused(string relativePath)
    {
        MediaPathGuard.TryResolve(_directory.Path, relativePath, isTarget: false, out _).Should().BeFalse();
    }

    [TestCase(".hidden/Song.mp3")]
    [TestCase("Artist/@eaDir/Song.mp3")]
    public void A_target_the_scan_would_ignore_is_refused(string relativePath)
    {
        MediaPathGuard.TryResolve(_directory.Path, relativePath, isTarget: true, out _).Should().BeFalse();
    }

    [Test]
    public void A_target_that_is_not_in_NFC_is_refused_but_an_existing_source_is_accepted()
    {
        string decomposed = "Café/Song.mp3";

        MediaPathGuard.TryResolve(_directory.Path, decomposed, isTarget: true, out _).Should().BeFalse();
        MediaPathGuard.TryResolve(_directory.Path, decomposed, isTarget: false, out _).Should().BeTrue();
    }

    [Test]
    public void A_folder_that_is_a_symbolic_link_is_refused()
    {
        using TemporaryDirectory outside = new();
        string link = Path.Combine(_directory.Path, "Linked");
        CreateDirectoryLinkOrIgnore(link, outside.Path);

        MediaPathGuard.TryResolve(_directory.Path, "Linked/Song.mp3", isTarget: false, out _).Should().BeFalse();
        MediaPathGuard.TryResolve(_directory.Path, "Linked/New/Song.mp3", isTarget: true, out _).Should().BeFalse();
        MediaPathGuard.IsLink(link).Should().BeTrue();
    }

    [Test]
    public void A_plain_folder_and_a_missing_entry_are_no_links()
    {
        string folder = _directory.CreateDirectory("Plain");

        MediaPathGuard.IsLink(folder).Should().BeFalse();
        MediaPathGuard.IsLink(Path.Combine(folder, "missing.mp3")).Should().BeFalse();
    }

    /// <summary>
    /// Symbolic links need a privilege on Windows (developer mode or admin); the Linux runs of the CI always have it.
    /// </summary>
    private static void CreateDirectoryLinkOrIgnore(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (OperatingSystem.IsWindows()
            && exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("Symbolic links cannot be created on this Windows machine: " + exception.Message);
        }
    }
}
