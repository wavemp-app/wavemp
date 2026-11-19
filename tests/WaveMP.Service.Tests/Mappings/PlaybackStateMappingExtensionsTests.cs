using AwesomeAssertions;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class PlaybackStateMappingExtensionsTests
{
    [Test]
    public void ToDto_Always_CarriesTheStateAndItsTracks()
    {
        TrackRow track = Track();
        var source = Guid.NewGuid();
        PlaybackStateView view = new(
            [new PlaybackStateEntry(track, false), new PlaybackStateEntry(track, true)],
            1,
            12_000,
            true,
            [1, 0],
            RepeatMode.One,
            new PlaybackSource(PlaybackSourceKind.SmartPlaylist, source),
            "phone",
            "Safari · iPhone",
            DateTimeOffset.UnixEpoch);

        PlaybackStateDto dto = view.ToDto();

        dto.Items.Select(item => item.Autoplay).Should().Equal(false, true);
        dto.Items[0].Track.Should().BeEquivalentTo(track.ToDto());
        dto.CurrentIndex.Should().Be(1);
        dto.PositionMilliseconds.Should().Be(12_000);
        dto.Shuffle.Should().BeTrue();
        dto.OriginalOrder.Should().Equal(1, 0);
        dto.Repeat.Should().Be(RepeatModeDto.One);
        dto.Source.Should().BeEquivalentTo(new { Kind = PlaybackSourceKindDto.SmartPlaylist, Id = source });
        dto.Device.Should().BeEquivalentTo(new { Id = "phone", Name = "Safari · iPhone" });
        dto.ChangedAt.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Test]
    public void ToDto_WithoutSource_HasNone()
    {
        PlaybackStateView view = new([], -1, 0, false, [], RepeatMode.Off, null, "phone", "Phone", default);

        view.ToDto().Source.Should().BeNull();
    }

    [Test]
    public void ToCommand_MissingOptionalValues_UsesTheDefaults()
    {
        var trackId = Guid.NewGuid();
        UpdatePlaybackStateDto request = new()
        {
            Items = [new UpdatePlaybackStateItemDto { TrackId = trackId }],
            CurrentIndex = 0,
            PositionMilliseconds = 500,
            Device = new PlaybackDeviceDto { Id = "phone", Name = "Phone" },
        };

        SavePlaybackStateCommand command = request.ToCommand();

        command.Items.Should().Equal(new PlaybackStateItem(trackId, false));
        command.CurrentIndex.Should().Be(0);
        command.PositionMilliseconds.Should().Be(500);
        command.Shuffle.Should().BeFalse();
        command.OriginalOrder.Should().BeEmpty();
        command.Repeat.Should().Be(RepeatMode.Off);
        command.Source.Should().BeNull();
        command.DeviceId.Should().Be("phone");
        command.DeviceName.Should().Be("Phone");
        command.ChangedAt.Should().BeNull();
    }

    [Test]
    public void ToCommand_AllValues_CarriesThem()
    {
        var trackId = Guid.NewGuid();
        var source = Guid.NewGuid();
        UpdatePlaybackStateDto request = new()
        {
            Items = [new UpdatePlaybackStateItemDto { TrackId = trackId, Autoplay = true }],
            CurrentIndex = 0,
            PositionMilliseconds = 500,
            Shuffle = true,
            OriginalOrder = [0],
            Repeat = RepeatModeDto.All,
            Source = new PlaybackSourceDto { Kind = PlaybackSourceKindDto.Artist, Id = source },
            Device = new PlaybackDeviceDto { Id = "phone", Name = "Phone" },
            ChangedAt = DateTimeOffset.UnixEpoch,
        };

        SavePlaybackStateCommand command = request.ToCommand();

        command.Items.Should().Equal(new PlaybackStateItem(trackId, true));
        command.Shuffle.Should().BeTrue();
        command.OriginalOrder.Should().Equal(0);
        command.Repeat.Should().Be(RepeatMode.All);
        command.Source.Should().Be(new PlaybackSource(PlaybackSourceKind.Artist, source));
        command.ChangedAt.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [TestCase(RepeatMode.Off, RepeatModeDto.Off)]
    [TestCase(RepeatMode.All, RepeatModeDto.All)]
    [TestCase(RepeatMode.One, RepeatModeDto.One)]
    public void RepeatMode_Always_MapsBothWays(RepeatMode mode, RepeatModeDto dto)
    {
        mode.ToDto().Should().Be(dto);
        dto.ToEntity().Should().Be(mode);
    }

    [TestCase(PlaybackSourceKind.Album, PlaybackSourceKindDto.Album)]
    [TestCase(PlaybackSourceKind.Artist, PlaybackSourceKindDto.Artist)]
    [TestCase(PlaybackSourceKind.Playlist, PlaybackSourceKindDto.Playlist)]
    [TestCase(PlaybackSourceKind.SmartPlaylist, PlaybackSourceKindDto.SmartPlaylist)]
    public void SourceKind_Always_MapsBothWays(PlaybackSourceKind kind, PlaybackSourceKindDto dto)
    {
        kind.ToDto().Should().Be(dto);
        dto.ToEntity().Should().Be(kind);
    }

    private static TrackRow Track()
    {
        return new TrackRow(
            Guid.NewGuid(),
            "Song",
            null,
            "Band",
            null,
            null,
            null,
            null,
            false,
            1,
            null,
            2001,
            null,
            TimeSpan.FromSeconds(3),
            true,
            false,
            false,
            DateTimeOffset.UnixEpoch);
    }
}
