using AwesomeAssertions;
using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Tests.Plans;

[TestFixture]
public class PlanTests
{
    private static readonly DateTimeOffset _time = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _music = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid _videos = new("00000000-0000-0000-0000-00000000000b");

    [Test]
    public void ScanScope_WithNestedFolders_KeepsOnlyTheOuterOnes()
    {
        var scope = ScanScope.Of(
            _music,
            ["Artist/Album", "Artist", "Other\\Album/", "Artist/Album/CD1", "Other/Album"]);

        scope.DirectoriesOf(_music).Should().Equal("Artist", "Other/Album");
        scope.IsFull.Should().BeFalse();
        scope.Contains(_music, "Artist/Album").Should().BeTrue();
        scope.Contains(_music, "Artists").Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("/")]
    [TestCase(".")]
    public void ScanScope_WithTheFolderOfTheSource_CoversTheWholeSource(string root)
    {
        var scope = ScanScope.Of(_music, ["Artist", root]);

        scope.IsFull.Should().BeFalse();
        scope.DirectoriesOf(_music).Should().Equal(string.Empty);
        scope.Contains(_music, "Anything/Deep").Should().BeTrue();
        ScanScope.Of(_music, []).DirectoriesOf(_music).Should().Equal(string.Empty);
    }

    [Test]
    public void ScanScope_FoldersOfSeveralSources_KeepsThemApart()
    {
        var scope = ScanScope.Of(
        [
            new SourceFolder(_videos, "Clips"),
            new SourceFolder(_music, "Artist"),
            new SourceFolder(_videos, "Clips/Live"),
        ]);

        scope.SourceIds.Should().BeEquivalentTo([_music, _videos]);
        scope.Folders.Should().Equal(new SourceFolder(_music, "Artist"), new SourceFolder(_videos, "Clips"));
        scope.Contains(_videos, "Artist").Should().BeFalse();
        scope.Contains(_music, "Clips").Should().BeFalse();
        scope.DirectoriesOf(Guid.NewGuid()).Should().BeNull();
    }

    [Test]
    public void ScanScope_Full_CoversEverySource()
    {
        ScanScope.Full.IsFull.Should().BeTrue();
        ScanScope.Full.DirectoriesOf(_videos).Should().Equal(string.Empty);
        ScanScope.Full.Contains(_music, "Artist").Should().BeTrue();
        ScanScope.OfSources([_videos]).DirectoriesOf(_videos).Should().Equal(string.Empty);
        ScanScope.OfSources([_videos]).DirectoriesOf(_music).Should().BeNull();
    }

    [Test]
    public void Plan_NewChangedUnchangedAndRemovedFiles_ListsTheChangedFolderOnly()
    {
        DiscoveredDirectory changed = Directory(
            "A",
            File("A/new.mp3"),
            File("A/changed.mp3", size: 2),
            File("A/same.mp3"),
            File("A/newer.mp3", modifiedAt: _time.AddMinutes(1)));
        DiscoveredDirectory unchanged = Directory("B", File("B/same.mp3"));
        MediaFileSnapshot[] stored =
        [
            Stored("A/changed.mp3"),
            Stored("A/same.mp3"),
            Stored("A/newer.mp3"),
            Stored("A/gone.mp3"),
            Stored("B/same.mp3"),
        ];

        ScanPlan plan = ScanPlanner.Plan([changed, unchanged], stored, 1);

        DirectoryPlan directory = plan.ChangedDirectories.Should().ContainSingle().Subject;
        directory.RelativePath.Should().Be("A");
        directory.FilesToRead.Select(file => file.RelativePath)
            .Should().Equal("A/new.mp3", "A/changed.mp3", "A/newer.mp3");
        (directory.Added, directory.Updated, directory.Removed).Should().Be((1, 2, 1));
        plan.FilesUnchanged.Should().Be(2);
        plan.FilesFound.Should().Be(5);
        plan.DirectoriesFound.Should().Be(2);
        plan.DeletedDirectories.Should().BeEmpty();
    }

    [Test]
    public void Plan_FileReadByAnOlderReaderOrWithOtherKind_IsReadAgain()
    {
        ScanPlan plan = ScanPlanner.Plan(
            [Directory("A", File("A/old.mp3"), File("A/kind.mp4"))],
            [Stored("A/old.mp3", version: 0), Stored("A/kind.mp4", kind: MediaFileKind.Video)],
            1);

        plan.ChangedDirectories.Single().FilesToRead.Select(file => file.RelativePath)
            .Should().Equal("A/old.mp3", "A/kind.mp4");
    }

    [Test]
    public void Plan_VersionPerKind_ReadsOnlyTheKindsWithANewerVersionAgain()
    {
        ScanPlan plan = ScanPlanner.Plan(
            [Directory(
                "A",
                File("A/song.mp3"),
                File("A/song.mkv", kind: MediaFileKind.Video),
                File("A/song.jpg", kind: MediaFileKind.Image))],
            [
                Stored("A/song.mp3"),
                Stored("A/song.mkv", kind: MediaFileKind.Video),
                Stored("A/song.jpg", kind: MediaFileKind.Image),
            ],
            ScannerVersions.MetadataOf);

        plan.ChangedDirectories.Single().FilesToRead.Select(file => file.RelativePath)
            .Should().Equal("A/song.mp3", "A/song.mkv");
        plan.FilesUnchanged.Should().Be(1);
    }

    [TestCase(MediaFileKind.Audio, 2)]
    [TestCase(MediaFileKind.Video, 2)]
    [TestCase(MediaFileKind.Image, 1)]
    public void MetadataOf_Kind_ReturnsTheVersionOfTheKind(MediaFileKind kind, int expected)
    {
        ScannerVersions.MetadataOf(kind).Should().Be(expected);
    }

    [Test]
    public void Plan_FileKnownToBeChanged_IsReadAgainWithTheSameSizeAndTime()
    {
        ScanPlan plan = ScanPlanner.Plan(
            [Directory("A", File("A/edited.mp3"), File("A/same.mp3"))],
            [Stored("A/edited.mp3"), Stored("A/same.mp3")],
            1,
            new HashSet<string>(["A/edited.mp3", "B/other.mp3"], StringComparer.Ordinal));

        DirectoryPlan directory = plan.ChangedDirectories.Should().ContainSingle().Subject;
        directory.FilesToRead.Select(file => file.RelativePath).Should().Equal("A/edited.mp3");
        (directory.Added, directory.Updated, directory.Removed).Should().Be((0, 1, 0));
        plan.FilesUnchanged.Should().Be(1);
    }

    [Test]
    public void Plan_StoredFolderMissingOnDisk_IsDeleted()
    {
        ScanPlan plan = ScanPlanner.Plan(
            [Directory("A", File("A/same.mp3"))],
            [Stored("A/same.mp3"), Stored("Gone/one.mp3"), Stored("Gone/two.mp3")],
            1);

        plan.DeletedDirectories.Should().Equal("Gone");
        plan.FilesInDeletedDirectories.Should().Be(2);
        plan.ChangedDirectories.Should().BeEmpty();
    }

    [Test]
    public void Plan_FolderThatLostAllFiles_IsChanged()
    {
        ScanPlan plan = ScanPlanner.Plan([Directory("A")], [Stored("A/one.mp3")], 1);

        plan.ChangedDirectories.Single().Removed.Should().Be(1);
    }

    private static DiscoveredDirectory Directory(string path, params DiscoveredFile[] files)
    {
        return new DiscoveredDirectory(path, files);
    }

    private static DiscoveredFile File(
        string path,
        long size = 1,
        DateTimeOffset? modifiedAt = null,
        MediaFileKind kind = MediaFileKind.Audio)
    {
        return new DiscoveredFile(path, path[(path.LastIndexOf('/') + 1)..], kind, size, modifiedAt ?? _time);
    }

    private static MediaFileSnapshot Stored(
        string path,
        int version = 1,
        MediaFileKind kind = MediaFileKind.Audio)
    {
        return new MediaFileSnapshot(Guid.NewGuid(), path, path[..path.LastIndexOf('/')], kind, 1, _time, version);
    }
}
