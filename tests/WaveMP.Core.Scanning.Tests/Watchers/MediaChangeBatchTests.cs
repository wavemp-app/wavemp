using AwesomeAssertions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Watchers;

namespace WaveMP.Core.Scanning.Tests.Watchers;

[TestFixture]
public class MediaChangeBatchTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _debounce = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(30);
    private static readonly Guid _music = Guid.NewGuid();
    private static readonly Guid _videos = Guid.NewGuid();

    [Test]
    public void IsDue_EmptyBatch_IsNever()
    {
        MediaChangeBatch batch = new();

        batch.IsDue(_start.AddHours(1), _debounce, _maxDelay).Should().BeFalse();
    }

    [Test]
    public void IsDue_AfterTheDebounceWithoutFurtherChanges_IsDue()
    {
        MediaChangeBatch batch = new();
        batch.Add(_music, "Artist/Album", _start);
        batch.Add(_videos, "Artist/Album", _start.AddSeconds(3));

        batch.IsDue(_start.AddSeconds(7), _debounce, _maxDelay).Should().BeFalse();
        batch.IsDue(_start.AddSeconds(8), _debounce, _maxDelay).Should().BeTrue();
    }

    [Test]
    public void IsDue_WithChangesComingIn_IsDueAfterTheMaximumDelay()
    {
        MediaChangeBatch batch = new();
        for (int second = 0; second <= 30; second += 2)
        {
            batch.Add(_music, "Busy", _start.AddSeconds(second));
        }

        batch.IsDue(_start.AddSeconds(29), _debounce, _maxDelay).Should().BeFalse();
        batch.IsDue(_start.AddSeconds(30), _debounce, _maxDelay).Should().BeTrue();
    }

    [Test]
    public void Take_ChangedFolders_ReducesThemToAScopePerSourceAndEmptiesTheBatch()
    {
        MediaChangeBatch batch = new();
        batch.Add(_music, "Artist/Album", _start);
        batch.Add(_music, "Artist", _start);
        batch.Add(_music, "Other\\Folder", _start);
        batch.Add(_videos, "Artist/Album", _start);

        ScanScope scope = batch.Take();

        scope.IsFull.Should().BeFalse();
        scope.DirectoriesOf(_music).Should().Equal("Artist", "Other/Folder");
        scope.DirectoriesOf(_videos).Should().Equal("Artist/Album");
        batch.IsEmpty.Should().BeTrue();
        batch.FirstChange.Should().BeNull();
    }

    [Test]
    public void Take_AfterARequestForASource_ScansTheWholeSourceOnly()
    {
        MediaChangeBatch batch = new();
        batch.Add(_music, "Artist", _start);
        batch.Add(_videos, "Clips", _start);
        batch.RequestSource(_music, _start);

        ScanScope scope = batch.Take();

        scope.IsFull.Should().BeFalse();
        scope.DirectoriesOf(_music).Should().Equal(string.Empty);
        scope.DirectoriesOf(_videos).Should().Equal("Clips");
    }

    [Test]
    public void Restore_ScopeThatCouldNotStart_KeepsItsFirstChange()
    {
        MediaChangeBatch batch = new();
        batch.Add(_music, "Artist", _start);
        ScanScope scope = batch.Take();
        batch.Add(_music, "Other", _start.AddSeconds(10));

        batch.Restore(scope, _start);

        batch.FirstChange.Should().Be(_start);
        batch.Take().DirectoriesOf(_music).Should().Equal("Artist", "Other");
    }

    [Test]
    public void Keep_SourceNoLongerWatched_DropsItsFolders()
    {
        MediaChangeBatch batch = new();
        batch.Add(_music, "Artist", _start);
        batch.Add(_videos, "Clips", _start);

        batch.Keep(new HashSet<Guid> { _music });

        batch.Take().Folders.Should().Equal(new SourceFolder(_music, "Artist"));
    }

    [Test]
    public void Keep_NoWatchedSourceLeft_EmptiesTheBatch()
    {
        MediaChangeBatch batch = new();
        batch.Add(_videos, "Clips", _start);

        batch.Keep(new HashSet<Guid>());

        batch.IsEmpty.Should().BeTrue();
        batch.FirstChange.Should().BeNull();
    }
}
