using AwesomeAssertions;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Streaming.Tests.PlaybackStates;

[TestFixture]
public class PlaybackStateRulesTests
{
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(2)]
    public void Validate_CurrentIndexInTheQueueOrNone_IsValid(int currentIndex)
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, currentIndex));

        status.Should().BeNull();
    }

    [TestCase(-2)]
    [TestCase(3)]
    public void Validate_CurrentIndexOutsideTheQueue_IsInvalid(int currentIndex)
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, currentIndex));

        status.Should().Be(PlaybackStateSaveStatus.InvalidCurrentIndex);
    }

    [Test]
    public void Validate_EmptyQueueWithCurrentEntry_IsInvalid()
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(0, 0));

        status.Should().Be(PlaybackStateSaveStatus.InvalidCurrentIndex);
    }

    [Test]
    public void Validate_ShuffledWithEveryPositionOnce_IsValid()
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, 0, shuffle: true, order: [2, 0, 1]));

        status.Should().BeNull();
    }

    [Test]
    public void Validate_ShuffledWithoutOrder_IsValid()
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, 0, shuffle: true, order: []));

        status.Should().BeNull();
    }

    [TestCase(new[] { 0, 1 })]
    [TestCase(new[] { 0, 1, 1 })]
    [TestCase(new[] { 0, 1, 3 })]
    [TestCase(new[] { 0, 1, 2, 2 })]
    public void Validate_ShuffledWithBrokenOrder_IsInvalid(int[] order)
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, 0, shuffle: true, order: order));

        status.Should().Be(PlaybackStateSaveStatus.InvalidOriginalOrder);
    }

    [Test]
    public void Validate_NotShuffled_IgnoresTheOrder()
    {
        PlaybackStateSaveStatus? status = PlaybackStateRules.Validate(Command(3, 0, shuffle: false, order: [7]));

        status.Should().BeNull();
    }

    [Test]
    public void Remaining_AllTracksExist_KeepsTheQueue()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(3, 1, [2], [2, 0, 1], _ => true);

        queue.Positions.Should().Equal(0, 1, 2);
        queue.CurrentIndex.Should().Be(1);
        queue.CurrentKept.Should().BeTrue();
        queue.AutoplayPositions.Should().Equal(2);
        queue.OriginalOrder.Should().Equal(2, 0, 1);
    }

    [Test]
    public void Remaining_EntryBeforeTheCurrentGone_MovesTheCurrentEntryUp()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(4, 2, [3], [3, 2, 1, 0], position => position != 0);

        queue.Positions.Should().Equal(1, 2, 3);
        queue.CurrentIndex.Should().Be(1);
        queue.CurrentKept.Should().BeTrue();
        queue.AutoplayPositions.Should().Equal(2);
        queue.OriginalOrder.Should().Equal(2, 1, 0);
    }

    [Test]
    public void Remaining_CurrentEntryGone_TheNextEntryBecomesCurrent()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(4, 1, [], [], position => position != 1);

        queue.Positions.Should().Equal(0, 2, 3);
        queue.CurrentIndex.Should().Be(1);
        queue.CurrentKept.Should().BeFalse();
    }

    [Test]
    public void Remaining_LastEntryCurrentAndGone_ThePreviousEntryBecomesCurrent()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(3, 2, [2], [], position => position != 2);

        queue.Positions.Should().Equal(0, 1);
        queue.CurrentIndex.Should().Be(1);
        queue.CurrentKept.Should().BeFalse();
        queue.AutoplayPositions.Should().BeEmpty();
    }

    [Test]
    public void Remaining_AllTracksGone_LeavesAnEmptyQueue()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(2, 0, [1], [1, 0], _ => false);

        queue.Positions.Should().BeEmpty();
        queue.CurrentIndex.Should().Be(-1);
        queue.CurrentKept.Should().BeFalse();
        queue.AutoplayPositions.Should().BeEmpty();
        queue.OriginalOrder.Should().BeEmpty();
    }

    [Test]
    public void Remaining_WithoutCurrentEntry_KeepsNone()
    {
        RemainingQueue queue = PlaybackStateRules.Remaining(2, -1, [], [], _ => true);

        queue.CurrentIndex.Should().Be(-1);
        queue.CurrentKept.Should().BeFalse();
    }

    private static SavePlaybackStateCommand Command(
        int count,
        int currentIndex,
        bool shuffle = false,
        IReadOnlyList<int>? order = null)
    {
        return new SavePlaybackStateCommand(
            [.. Enumerable.Range(0, count).Select(_ => new PlaybackStateItem(Guid.NewGuid(), false))],
            currentIndex,
            0,
            shuffle,
            order ?? [],
            RepeatMode.Off,
            null,
            "device",
            "Device",
            null);
    }
}
