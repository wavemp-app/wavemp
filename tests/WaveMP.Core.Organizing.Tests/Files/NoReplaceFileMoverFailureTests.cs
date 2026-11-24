using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

public sealed class NoReplaceFileMoverFailureTests
{
    [Test]
    public void A_failure_before_the_source_is_deleted_removes_the_copy_and_keeps_the_source()
    {
        using TemporaryDirectory directory = new();
        string source = directory.Combine("Song.mp3");
        File.WriteAllBytes(source, [1, 2, 3]);
        string target = Path.Combine(directory.CreateDirectory("b"), "Song.mp3");
        string? copied = null;
        NoReplaceFileMover mover = new(NoReplaceStrategy.Copy)
        {
            BeforeSourceDelete = path =>
            {
                copied = path;
                File.Exists(path).Should().BeTrue();
                throw new IOException("Simulated failure.");
            },
        };

        FileMoveResult result = mover.Move(source, target);

        result.Status.Should().Be(FileMoveStatus.Failed);
        copied.Should().Be(target);
        File.Exists(target).Should().BeFalse();
        File.ReadAllBytes(source).Should().Equal([1, 2, 3]);
    }

    [Test]
    public void A_case_only_rename_is_done_by_the_rename_of_the_system()
    {
        using TemporaryDirectory directory = new();
        string source = directory.Combine("song.mp3");
        File.WriteAllBytes(source, [1, 2, 3]);
        string target = directory.Combine("Song.mp3");
        NoReplaceFileMover mover = new(NoReplaceStrategy.Rename);
        bool caseInsensitive = File.Exists(target);

        FileMoveResult result = mover.Move(source, target);

        if (caseInsensitive && !OperatingSystem.IsWindows())
        {
            // renameat2 sees the same file; the planner moves through a temporary name there.
            result.Status.Should().NotBe(FileMoveStatus.Failed);
        }
        else
        {
            result.Should().Be(FileMoveResult.Moved);
            Directory.GetFiles(directory.Path).Select(Path.GetFileName).Should().Equal("Song.mp3");
        }

        File.ReadAllBytes(target).Should().Equal([1, 2, 3]);
    }
}
