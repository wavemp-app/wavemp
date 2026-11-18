using AwesomeAssertions;
using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class ScrobblingMappingExtensionsTests
{
    private static readonly DateTimeOffset _connectedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ToDto_NotConnected_HasNoAccount()
    {
        ScrobbleServiceState state = new(ScrobbleService.ListenBrainz, true, null, 0, null);

        ScrobblingServiceDto dto = state.ToDto();

        dto.Should().BeEquivalentTo(new ScrobblingServiceDto
        {
            Service = ScrobbleServiceDto.ListenBrainz,
            IsAvailable = true,
            Status = ScrobbleConnectionStatusDto.NotConnected,
            QueuedScrobbles = 0,
        });
    }

    [TestCase(ScrobbleConnectionStatus.Active, ScrobbleConnectionStatusDto.Connected)]
    [TestCase(ScrobbleConnectionStatus.AuthorizationFailed, ScrobbleConnectionStatusDto.AuthorizationFailed)]
    public void ToDto_Connected_TakesTheAccountButNeverTheCredential(
        ScrobbleConnectionStatus status,
        ScrobbleConnectionStatusDto expected)
    {
        ScrobbleConnection connection = new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Service = ScrobbleService.LastFm,
            AccountName = "listener",
            ProtectedCredential = "secret",
            Status = status,
            ConnectedAt = _connectedAt,
            LastScrobbledAt = _connectedAt.AddHours(1),
        };
        Uri page = new("https://www.last.fm/api/auth/?api_key=key");
        ScrobbleServiceState state = new(ScrobbleService.LastFm, true, connection, 4, page);

        ScrobblingServiceDto dto = state.ToDto();

        dto.Should().BeEquivalentTo(new ScrobblingServiceDto
        {
            Service = ScrobbleServiceDto.LastFm,
            IsAvailable = true,
            Status = expected,
            AccountName = "listener",
            ConnectedAt = _connectedAt,
            LastScrobbledAt = _connectedAt.AddHours(1),
            QueuedScrobbles = 4,
            AuthorizationUrl = page,
        });
    }

    [TestCase(ScrobbleServiceDto.LastFm, ScrobbleService.LastFm)]
    [TestCase(ScrobbleServiceDto.ListenBrainz, ScrobbleService.ListenBrainz)]
    public void ToEntity_Service_MapsBothWays(ScrobbleServiceDto dto, ScrobbleService entity)
    {
        dto.ToEntity().Should().Be(entity);
        entity.ToDto().Should().Be(dto);
    }
}
