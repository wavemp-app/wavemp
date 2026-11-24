using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

[TestFixture(NoReplaceStrategy.Rename)]
[TestFixture(NoReplaceStrategy.Link)]
[TestFixture(NoReplaceStrategy.Copy)]
public sealed class NoReplaceFileMoverTests(NoReplaceStrategy strategy)
{
    private static readonly byte[] _sourceBytes = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] _targetBytes = [9, 9, 9];

    private TemporaryDirectory _directory = null!;
    private NoReplaceFileMover _mover = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new TemporaryDirectory();
        _mover = new NoReplaceFileMover(strategy);
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Dispose();
    }

    [Test]
    public void Move_moves_the_file_with_its_content_and_time()
    {
        string source = WriteFile("a/Song.mp3", _sourceBytes);
        DateTime modified = new(2020, 5, 17, 10, 11, 12, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(source, modified);
        string target = Path.Combine(_directory.CreateDirectory("b"), "Song.mp3");

        FileMoveResult result = _mover.Move(source, target);

        result.Should().Be(FileMoveResult.Moved);
        File.Exists(source).Should().BeFalse();
        File.ReadAllBytes(target).Should().Equal(_sourceBytes);
        File.GetLastWriteTimeUtc(target).Should().BeCloseTo(modified, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Move_never_replaces_an_existing_target()
    {
        string source = WriteFile("a/Song.mp3", _sourceBytes);
        string target = WriteFile("b/Song.mp3", _targetBytes);

        FileMoveResult result = _mover.Move(source, target);

        result.Should().Be(FileMoveResult.TargetExists);
        File.ReadAllBytes(source).Should().Equal(_sourceBytes);
        File.ReadAllBytes(target).Should().Equal(_targetBytes);
    }

    [Test]
    public void Move_refuses_a_target_that_appeared_after_the_check()
    {
        // The planner checked the target before; the file that shows up afterwards is still not replaced.
        string source = WriteFile("a/Song.mp3", _sourceBytes);
        string target = Path.Combine(_directory.CreateDirectory("b"), "Song.mp3");
        File.Exists(target).Should().BeFalse();
        File.WriteAllBytes(target, _targetBytes);

        FileMoveResult result = _mover.Move(source, target);

        result.Should().Be(FileMoveResult.TargetExists);
        File.ReadAllBytes(source).Should().Equal(_sourceBytes);
        File.ReadAllBytes(target).Should().Equal(_targetBytes);
    }

    [Test]
    public void Move_refuses_a_target_that_differs_only_in_case_on_a_case_insensitive_system()
    {
        string source = WriteFile("a/Song.mp3", _sourceBytes);
        string other = WriteFile("b/song.mp3", _targetBytes);
        string target = Path.Combine(_directory.Path, "b", "SONG.mp3");
        bool caseInsensitive = File.Exists(target);

        FileMoveResult result = _mover.Move(source, target);

        if (caseInsensitive)
        {
            result.Should().Be(FileMoveResult.TargetExists);
            File.ReadAllBytes(source).Should().Equal(_sourceBytes);
        }
        else
        {
            result.Should().Be(FileMoveResult.Moved);
        }

        File.ReadAllBytes(other).Should().Equal(_targetBytes);
    }

    [Test]
    public void Move_reports_a_missing_source()
    {
        string target = Path.Combine(_directory.CreateDirectory("b"), "Song.mp3");

        FileMoveResult result = _mover.Move(Path.Combine(_directory.Path, "a", "Song.mp3"), target);

        result.Should().Be(FileMoveResult.SourceMissing);
        File.Exists(target).Should().BeFalse();
    }

    [Test]
    public void Move_fails_without_a_trace_when_the_target_folder_is_missing()
    {
        string source = WriteFile("a/Song.mp3", _sourceBytes);
        string target = Path.Combine(_directory.Path, "missing", "Song.mp3");

        FileMoveResult result = _mover.Move(source, target);

        result.Status.Should().Be(FileMoveStatus.Failed);
        File.ReadAllBytes(source).Should().Equal(_sourceBytes);
        Directory.Exists(Path.Combine(_directory.Path, "missing")).Should().BeFalse();
    }

    [Test]
    public void Copy_keeps_the_source_and_never_replaces()
    {
        string source = WriteFile("cover.jpg", _sourceBytes);
        string target = Path.Combine(_directory.CreateDirectory("b"), "cover.jpg");

        _mover.Copy(source, target).Should().Be(FileMoveResult.Moved);
        _mover.Copy(source, target).Should().Be(FileMoveResult.TargetExists);

        File.ReadAllBytes(source).Should().Equal(_sourceBytes);
        File.ReadAllBytes(target).Should().Equal(_sourceBytes);
    }

    private string WriteFile(string relativePath, byte[] content)
    {
        string path = _directory.Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }
}
