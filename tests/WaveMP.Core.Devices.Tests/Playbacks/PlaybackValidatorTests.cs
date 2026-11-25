using AwesomeAssertions;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Tests.Playbacks;

[TestFixture]
public class PlaybackValidatorTests
{
    private static readonly Guid[] _tracks = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    [Test]
    public void Validate_ValidState_IsValid()
    {
        PlaybackValidator.Validate(new PublishPlaybackCommand(true, DeviceTestContext.Playback(_tracks, 2)), 0)
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_EmptyQueueWithoutCurrentEntry_IsValid()
    {
        PlaybackValidator.Validate(new PublishPlaybackCommand(true, DeviceTestContext.Playback([], -1)), 0)
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_ClaimWithoutQueue_NeedsTheQueue()
    {
        LivePlaybackInput playback = DeviceTestContext.Playback(_tracks) with { Queue = null };

        PlaybackValidator.Validate(new PublishPlaybackCommand(true, playback), 3).Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("playback.queue", DeviceValidationCode.Required));
        PlaybackValidator.Validate(new PublishPlaybackCommand(false, playback), 3).Should().BeEmpty();
    }

    [Test]
    public void Validate_WithoutQueue_ChecksTheCurrentIndexAgainstTheLiveQueue()
    {
        LivePlaybackInput playback = DeviceTestContext.Playback(_tracks, 2) with { Queue = null };

        PlaybackValidator.Validate(new PublishPlaybackCommand(false, playback), 2).Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("playback.currentIndex", DeviceValidationCode.OutOfRange, -1, 1));
    }

    [Test]
    public void Validate_MoreThan2000Entries_IsTooLong()
    {
        Guid[] many = [.. Enumerable.Range(0, 2001).Select(_ => Guid.NewGuid())];

        PlaybackValidator.Validate(new PublishPlaybackCommand(true, DeviceTestContext.Playback(many)), 0)
            .Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("playback.queue.items", DeviceValidationCode.MaxItems, Max: 2000));
    }

    [Test]
    public void Validate_ShuffledQueueWithAnIncompleteOrder_IsInvalid()
    {
        LivePlaybackInput playback = DeviceTestContext.Playback(_tracks) with
        {
            Queue = new LiveQueueInput([.. _tracks.Select(id => new PlaybackStateItem(id, false))], true, [0, 0, 1], null),
        };

        PlaybackValidator.Validate(new PublishPlaybackCommand(true, playback), 0).Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("playback.queue.originalOrder", DeviceValidationCode.InvalidValue));
    }

    [Test]
    public void Validate_OutOfRangeNumbersAndUndefinedEnums_ReturnsEveryError()
    {
        LivePlaybackInput playback = DeviceTestContext.Playback(_tracks) with
        {
            Status = (LivePlaybackStatus)8,
            Repeat = (RepeatMode)8,
            PositionMilliseconds = -1,
            DurationMilliseconds = 86_400_001,
            Volume = double.PositiveInfinity,
            Queue = new LiveQueueInput(
                [.. _tracks.Select(id => new PlaybackStateItem(id, false))],
                false,
                [],
                new PlaybackSource((PlaybackSourceKind)8, Guid.NewGuid())),
        };

        IReadOnlyList<DeviceValidationError> errors =
            PlaybackValidator.Validate(new PublishPlaybackCommand(true, playback), 0);

        errors.Select(error => error.Field).Should().BeEquivalentTo(
        [
            "playback.status",
            "playback.repeat",
            "playback.positionMilliseconds",
            "playback.durationMilliseconds",
            "playback.volume",
            "playback.queue.source.kind",
        ]);
    }

    [Test]
    public void Validate_WithoutPlayback_IsRequired()
    {
        PlaybackValidator.Validate(new PublishPlaybackCommand(true, null!), 0).Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("playback", DeviceValidationCode.Required));
    }
}
