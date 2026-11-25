using AwesomeAssertions;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Tests.Commands;

[TestFixture]
public class CommandValidatorTests
{
    private const string Target = "laptop";

    [TestCase(DeviceCommandType.Play)]
    [TestCase(DeviceCommandType.Pause)]
    [TestCase(DeviceCommandType.Stop)]
    [TestCase(DeviceCommandType.Next)]
    [TestCase(DeviceCommandType.Previous)]
    public void Validate_CommandWithoutMembers_IsValid(DeviceCommandType type)
    {
        CommandValidator.Validate(new DeviceCommand(Target, type)).Should().BeEmpty();
    }

    [Test]
    public void Validate_InvalidTargetAndUndefinedType_ReturnsBothErrors()
    {
        IReadOnlyList<DeviceValidationError> errors =
            CommandValidator.Validate(new DeviceCommand("no target", (DeviceCommandType)99));

        errors.Should().BeEquivalentTo(
        [
            new DeviceValidationError("targetDeviceId", DeviceValidationCode.InvalidFormat),
            new DeviceValidationError("type", DeviceValidationCode.InvalidValue),
        ]);
    }

    [TestCase(null, DeviceValidationCode.Required)]
    [TestCase(-1, DeviceValidationCode.OutOfRange)]
    [TestCase(86_400_001, DeviceValidationCode.OutOfRange)]
    public void Validate_SeekWithInvalidPosition_ReturnsTheCode(int? position, DeviceValidationCode code)
    {
        IReadOnlyList<DeviceValidationError> errors = CommandValidator.Validate(
            new DeviceCommand(Target, DeviceCommandType.Seek, PositionMilliseconds: position));

        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = "positionMilliseconds", Code = code });
    }

    [Test]
    public void Validate_SeekToTheEndOfTheRange_IsValid()
    {
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.Seek, PositionMilliseconds: 86_400_000))
            .Should().BeEmpty();
    }

    [TestCase(null, DeviceValidationCode.Required)]
    [TestCase(-0.1, DeviceValidationCode.OutOfRange)]
    [TestCase(1.01, DeviceValidationCode.OutOfRange)]
    [TestCase(double.NaN, DeviceValidationCode.OutOfRange)]
    public void Validate_SetVolumeWithInvalidVolume_ReturnsTheCode(double? volume, DeviceValidationCode code)
    {
        IReadOnlyList<DeviceValidationError> errors =
            CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.SetVolume, Volume: volume));

        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = "volume", Code = code });
    }

    [TestCase(DeviceCommandType.SetShuffle)]
    [TestCase(DeviceCommandType.SetCrossfade)]
    [TestCase(DeviceCommandType.SetAutoplay)]
    public void Validate_SwitchWithoutEnabled_IsRequired(DeviceCommandType type)
    {
        CommandValidator.Validate(new DeviceCommand(Target, type)).Should().ContainSingle()
            .Which.Should().Be(new DeviceValidationError("enabled", DeviceValidationCode.Required));
        CommandValidator.Validate(new DeviceCommand(Target, type, Enabled: true)).Should().BeEmpty();
    }

    [Test]
    public void Validate_SetRepeat_NeedsADefinedMode()
    {
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.SetRepeat)).Single().Code
            .Should().Be(DeviceValidationCode.Required);
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.SetRepeat, Repeat: (RepeatMode)7))
            .Single().Code.Should().Be(DeviceValidationCode.InvalidValue);
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.SetRepeat, Repeat: RepeatMode.One))
            .Should().BeEmpty();
    }

    [TestCase(DeviceCommandType.PlayTracks)]
    [TestCase(DeviceCommandType.AddToQueue)]
    [TestCase(DeviceCommandType.PlayNext)]
    public void Validate_SongCommands_NeedOneTo2000Songs(DeviceCommandType type)
    {
        CommandValidator.Validate(new DeviceCommand(Target, type)).Single()
            .Should().Be(new DeviceValidationError("trackIds", DeviceValidationCode.Required));
        CommandValidator.Validate(new DeviceCommand(Target, type, TrackIds: [])).Single()
            .Should().Be(new DeviceValidationError("trackIds", DeviceValidationCode.MinItems, Min: 1));
        CommandValidator.Validate(new DeviceCommand(Target, type, TrackIds: [.. Enumerable.Range(0, 2001).Select(_ => Guid.NewGuid())]))
            .Single().Should().Be(new DeviceValidationError("trackIds", DeviceValidationCode.MaxItems, Max: 2000));
        CommandValidator.Validate(new DeviceCommand(Target, type, TrackIds: [.. Enumerable.Range(0, 2000).Select(_ => Guid.NewGuid())]))
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_PlayTracksWithStartOutsideTheSongsAndUndefinedSource_ReturnsBothErrors()
    {
        IReadOnlyList<DeviceValidationError> errors = CommandValidator.Validate(new DeviceCommand(
            Target,
            DeviceCommandType.PlayTracks,
            TrackIds: [Guid.NewGuid(), Guid.NewGuid()],
            StartIndex: 2,
            Source: new PlaybackSource((PlaybackSourceKind)9, Guid.NewGuid())));

        errors.Should().BeEquivalentTo(
        [
            new DeviceValidationError("startIndex", DeviceValidationCode.OutOfRange, 0, 1),
            new DeviceValidationError("source.kind", DeviceValidationCode.InvalidValue),
        ]);
    }

    [TestCase(DeviceCommandType.PlayQueueEntry)]
    [TestCase(DeviceCommandType.RemoveQueueEntry)]
    public void Validate_EntryCommands_NeedTheQueueVersionAndAnIndex(DeviceCommandType type)
    {
        CommandValidator.Validate(new DeviceCommand(Target, type)).Select(error => error.Field)
            .Should().BeEquivalentTo(["expectedQueueVersion", "index"]);
        CommandValidator.Validate(new DeviceCommand(Target, type, ExpectedQueueVersion: 5, Index: -1)).Single()
            .Should().Be(new DeviceValidationError("index", DeviceValidationCode.OutOfRange, Min: 0));
        CommandValidator.Validate(new DeviceCommand(Target, type, ExpectedQueueVersion: 5, Index: 0))
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_MoveQueueEntry_NeedsBothPositions()
    {
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.MoveQueueEntry, ExpectedQueueVersion: 1, Index: 0))
            .Single().Should().Be(new DeviceValidationError("toIndex", DeviceValidationCode.Required));
    }

    [Test]
    public void Validate_ClearQueue_NeedsTheQueueVersion()
    {
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.ClearQueue)).Single()
            .Should().Be(new DeviceValidationError("expectedQueueVersion", DeviceValidationCode.Required));
        CommandValidator.Validate(new DeviceCommand(Target, DeviceCommandType.ClearQueue, ExpectedQueueVersion: 3))
            .Should().BeEmpty();
    }

    [Test]
    public void UsesTracksAndUsesQueueVersion_Always_NameTheCommandsThatNeedThem()
    {
        Enum.GetValues<DeviceCommandType>().Where(CommandValidator.UsesTracks).Should().BeEquivalentTo(
            [DeviceCommandType.PlayTracks, DeviceCommandType.AddToQueue, DeviceCommandType.PlayNext]);
        Enum.GetValues<DeviceCommandType>().Where(CommandValidator.UsesQueueVersion).Should().BeEquivalentTo(
        [
            DeviceCommandType.PlayQueueEntry,
            DeviceCommandType.RemoveQueueEntry,
            DeviceCommandType.MoveQueueEntry,
            DeviceCommandType.ClearQueue,
        ]);
    }
}
