using AwesomeAssertions;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class DeviceHubMappingExtensionsTests
{
    [Test]
    public void CommandTypes_EveryValue_MapsToTheDtoOfTheSameNameAndBack()
    {
        Enum.GetValues<DeviceCommandType>().Should().HaveSameCount(Enum.GetValues<DeviceCommandTypeDto>());
        foreach (DeviceCommandType type in Enum.GetValues<DeviceCommandType>())
        {
            type.ToDto().ToString().Should().Be(type.ToString());
            type.ToDto().ToEntity().Should().Be(type);
        }
    }

    [Test]
    public void Statuses_EveryValue_MapsToTheDtoOfTheSameName()
    {
        Enum.GetValues<LivePlaybackStatus>().Should().HaveSameCount(Enum.GetValues<LivePlaybackStatusDto>());
        foreach (LivePlaybackStatus status in Enum.GetValues<LivePlaybackStatus>())
        {
            status.ToDto().ToString().Should().Be(status.ToString());
            status.ToDto().ToEntity().Should().Be(status);
        }

        Enum.GetValues<CommandStatus>().Should().HaveSameCount(Enum.GetValues<CommandStatusDto>());
        foreach (CommandStatus status in Enum.GetValues<CommandStatus>())
        {
            status.ToDto().ToString().Should().Be(status.ToString());
        }

        Enum.GetValues<PublishStatus>().Should().HaveSameCount(Enum.GetValues<PublishStatusDto>());
        foreach (PublishStatus status in Enum.GetValues<PublishStatus>())
        {
            status.ToDto().ToString().Should().Be(status.ToString());
        }
    }

    [Test]
    public void ToCommand_Command_KeepsEveryMember()
    {
        var track = Guid.NewGuid();
        var album = Guid.NewGuid();
        DeviceCommandDto request = new()
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.PlayTracks,
            ExpectedQueueVersion = 7,
            PositionMilliseconds = 1000,
            Volume = 0.5,
            Muted = true,
            Enabled = false,
            Repeat = RepeatModeDto.One,
            TrackIds = [track],
            StartIndex = 0,
            Source = new PlaybackSourceDto { Kind = PlaybackSourceKindDto.Album, Id = album },
            Shuffle = true,
            Index = 2,
            ToIndex = 3,
        };

        DeviceCommand command = request.ToCommand();

        command.Should().BeEquivalentTo(new DeviceCommand(
            "speaker",
            DeviceCommandType.PlayTracks,
            7,
            1000,
            0.5,
            true,
            false,
            RepeatMode.One,
            [track],
            0,
            new PlaybackSource(PlaybackSourceKind.Album, album),
            true,
            2,
            3));
    }

    [Test]
    public void ToCommand_PublicationWithoutQueue_KeepsTheQueueOut()
    {
        PublishPlaybackDto request = new()
        {
            Claim = false,
            Playback = new LivePlaybackInputDto
            {
                Status = LivePlaybackStatusDto.Paused,
                CurrentIndex = 0,
                PositionMilliseconds = 42,
                DurationMilliseconds = 100,
                Repeat = RepeatModeDto.Off,
                Volume = 1,
                Muted = false,
                Crossfade = true,
                Autoplay = true,
            },
        };

        PublishPlaybackCommand command = request.ToCommand();

        command.Playback.Should().BeEquivalentTo(new LivePlaybackInput(
            LivePlaybackStatus.Paused,
            null,
            0,
            42,
            100,
            RepeatMode.Off,
            1,
            false,
            true,
            true));
    }

    [Test]
    public void ToCommand_QueueWithoutOrder_HasAnEmptyOrder()
    {
        var track = Guid.NewGuid();
        PublishPlaybackDto request = new()
        {
            Claim = true,
            Playback = new LivePlaybackInputDto
            {
                Status = LivePlaybackStatusDto.Playing,
                Queue = new LiveQueueInputDto
                {
                    Items = [new LiveQueueItemDto { TrackId = track, Autoplay = true }],
                    Shuffle = true,
                },
                CurrentIndex = 0,
                PositionMilliseconds = 0,
                DurationMilliseconds = 0,
                Repeat = RepeatModeDto.All,
                Volume = 0.3,
                Muted = true,
                Crossfade = false,
                Autoplay = false,
            },
        };

        LiveQueueInput queue = request.ToCommand().Playback.Queue!;

        queue.Items.Should().Equal(new PlaybackStateItem(track, true));
        queue.Shuffle.Should().BeTrue();
        queue.OriginalOrder.Should().BeEmpty();
        queue.Source.Should().BeNull();
    }

    [Test]
    public void ToDto_LivePlayback_KeepsEveryMember()
    {
        var track = Guid.NewGuid();
        LivePlaybackView view = new(
            10,
            8,
            "speaker",
            LivePlaybackStatus.Playing,
            1,
            track,
            3,
            1234,
            5678,
            true,
            RepeatMode.All,
            new PlaybackSource(PlaybackSourceKind.Playlist, track),
            0.7,
            false,
            true,
            false,
            new PlaybackHandover("phone", true));

        LivePlaybackDto dto = view.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            Version = 10L,
            QueueVersion = 8L,
            ActiveDeviceId = "speaker",
            Status = LivePlaybackStatusDto.Playing,
            CurrentIndex = 1,
            CurrentTrackId = (Guid?)track,
            QueueLength = 3,
            PositionMilliseconds = 1234,
            DurationMilliseconds = 5678,
            Shuffle = true,
            Repeat = RepeatModeDto.All,
            Source = new { Kind = PlaybackSourceKindDto.Playlist, Id = track },
            Volume = 0.7,
            Muted = false,
            Crossfade = true,
            Autoplay = false,
            Handover = new { FromDeviceId = "phone", Play = true },
        });
    }

    [Test]
    public void ToDto_ResultWithErrors_KeepsCodesAndLimits()
    {
        CommandResult result = new(
            CommandStatus.Invalid,
            0,
            0,
            [
                new DeviceValidationError("index", DeviceValidationCode.OutOfRange, -1, 4),
                new DeviceValidationError("trackIds", DeviceValidationCode.Required),
            ]);

        CommandResultDto dto = result.ToDto();

        dto.Status.Should().Be(CommandStatusDto.Invalid);
        dto.Errors.Should().BeEquivalentTo(
        [
            new { Field = "index", Code = ValidationErrorCodeDto.OutOfRange, Min = (int?)-1, Max = (int?)4 },
            new { Field = "trackIds", Code = ValidationErrorCodeDto.Required, Min = (int?)null, Max = (int?)null },
        ]);
    }

    [Test]
    public void ToDto_RegistrationThatFailed_HasNoSnapshot()
    {
        RegisterResult result = new(null, [new DeviceValidationError("deviceId", DeviceValidationCode.InvalidFormat)]);

        RegisterResultDto dto = result.ToDto();

        dto.Status.Should().Be(RegisterStatusDto.Invalid);
        dto.Snapshot.Should().BeNull();
        dto.Errors.Should().ContainSingle().Which.Code.Should().Be(ValidationErrorCodeDto.InvalidFormat);
    }
}
