using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

public sealed class EmptyFolderCleanerTests
{
    [Test]
    public void Empty_folders_are_removed_up_to_the_first_folder_with_content_but_never_the_root()
    {
        using TemporaryDirectory directory = new();
        directory.CreateDirectory("Artist/Album/CD1");
        File.WriteAllText(directory.Combine("Artist/notes.txt"), "keep");

        IReadOnlyList<string> removed = EmptyFolderCleaner.RemoveEmptyFolders(directory.Path, "Artist/Album/CD1");

        removed.Should().Equal("Artist/Album/CD1", "Artist/Album");
        Directory.Exists(directory.Combine("Artist")).Should().BeTrue();
        File.Exists(directory.Combine("Artist/notes.txt")).Should().BeTrue();
    }

    [Test]
    public void The_root_stays_even_when_everything_is_empty()
    {
        using TemporaryDirectory directory = new();
        directory.CreateDirectory("A/B");

        IReadOnlyList<string> removed = EmptyFolderCleaner.RemoveEmptyFolders(directory.Path, "A/B");

        removed.Should().Equal("A/B", "A");
        Directory.Exists(directory.Path).Should().BeTrue();
    }

    [Test]
    public void A_folder_with_a_hidden_file_stays()
    {
        using TemporaryDirectory directory = new();
        directory.CreateDirectory("A");
        File.WriteAllText(directory.Combine("A/.nomedia"), string.Empty);

        EmptyFolderCleaner.RemoveEmptyFolders(directory.Path, "A").Should().BeEmpty();
        File.Exists(directory.Combine("A/.nomedia")).Should().BeTrue();
    }

    [Test]
    public void A_path_outside_the_source_is_never_touched()
    {
        using TemporaryDirectory root = new();
        using TemporaryDirectory outside = new();
        string name = Path.GetFileName(outside.Path);

        EmptyFolderCleaner.RemoveEmptyFolders(root.Path, "../" + name).Should().BeEmpty();
        Directory.Exists(outside.Path).Should().BeTrue();
    }
}
