using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

public sealed class FolderNamesTests
{
    [TestCase("Björk", "BJÖRK")]
    [TestCase("Björk", "björk")]
    [TestCase("ABBA", "abba")]
    public void Names_that_differ_only_in_case_or_normalization_are_the_same(string left, string right)
    {
        FolderNames.AreSame(left, right).Should().BeTrue();
    }

    [TestCase("Song", "Song 2")]
    [TestCase("Bjork", "Björk")]
    public void Different_names_are_not_the_same(string left, string right)
    {
        FolderNames.AreSame(left, right).Should().BeFalse();
    }

    [TestCase("Song.mp3", "Song", "mp3")]
    [TestCase("Song.Live.flac", "Song.Live", "flac")]
    [TestCase(".hidden", ".hidden", null)]
    [TestCase("Song.", "Song", null)]
    [TestCase("Song", "Song", null)]
    public void Base_name_and_extension_are_split_at_the_last_dot(string name, string baseName, string? extension)
    {
        FolderNames.BaseNameOf(name).Should().Be(baseName);
        FolderNames.ExtensionOf(name).Should().Be(extension);
    }

    [TestCase("A/B/Song.mp3", "A/B", "Song.mp3")]
    [TestCase("Song.mp3", "", "Song.mp3")]
    public void Relative_paths_are_split_and_combined(string path, string directory, string fileName)
    {
        FolderNames.DirectoryOf(path).Should().Be(directory);
        FolderNames.FileNameOf(path).Should().Be(fileName);
        FolderNames.Combine(directory, fileName).Should().Be(path);
    }
}
