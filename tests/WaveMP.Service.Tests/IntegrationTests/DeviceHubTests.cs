using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Users.Sessions;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Hubs;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Service.Tokens;
using WaveMP.Testing.Seeds;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The real-time connection of the devices through the official SignalR client against the in-memory server: the
/// credentials it accepts, the devices of a profile, commands, hand-overs and conflicts, the limits of its messages,
/// and that a profile never sees or controls the devices of another one.
/// </summary>
[TestFixture]
[Category("Integration")]
public class DeviceHubTests
{
    private const string NegotiatePath = DeviceHubDefaults.Path + "/negotiate?negotiateVersion=1";
    private const string TicketPath = "/api/v1/me/device-hub-tickets";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private Track _first = null!;
    private Track _second = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_device_hub",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        LibrarySeed seed = new();
        Artist artist = seed.Artist("Hub Artist");
        Album album = seed.Album("Hub Album", artist);
        _first = seed.Track("First Hub Song", artist, album, 1, duration: TimeSpan.FromMinutes(3));
        _second = seed.Track("Second Hub Song", artist, album, 2, duration: TimeSpan.FromMinutes(4));
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        await seed.SaveAsync(scope.ServiceProvider.GetRequiredService<WaveMpDbContext>());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Negotiate_WithoutCredentials_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _anonymous.PostAsync(NegotiatePath, null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task Negotiate_WithTicketOrSessionToken_Succeeds()
    {
        Profile profile = await CreateProfileAsync();

        HttpResponseMessage ticketInQuery = await _anonymous.PostAsync(
            DeviceHubClient.WithQuery(
                new Uri(_anonymous.BaseAddress!, NegotiatePath),
                DeviceHubDefaults.AccessTokenQueryParameter,
                await TicketAsync(profile)),
            null);
        HttpResponseMessage ticketInHeader = await NegotiateWithHeaderAsync(await TicketAsync(profile));
        HttpResponseMessage sessionTokenInHeader = await NegotiateWithHeaderAsync(profile.Token);

        ticketInQuery.StatusCode.Should().Be(HttpStatusCode.OK);
        ticketInHeader.StatusCode.Should().Be(HttpStatusCode.OK);
        sessionTokenInHeader.StatusCode.Should().Be(HttpStatusCode.OK);
        (await sessionTokenInHeader.Content.ReadFromJsonAsync<JsonObject>())!["connectionToken"]!
            .GetValue<string>().Should().NotBeEmpty();
    }

    [Test]
    public async Task SessionToken_InTheQueryString_IsRejected()
    {
        Profile profile = await CreateProfileAsync();
        Uri negotiate = DeviceHubClient.WithQuery(
            new Uri(_anonymous.BaseAddress!, NegotiatePath),
            DeviceHubDefaults.AccessTokenQueryParameter,
            profile.Token);
        Uri webSocket = new(_factory.Server.BaseAddress, DeviceHubDefaults.Path);

        HttpResponseMessage response = await _anonymous.PostAsync(negotiate, null);
        Func<Task> withSessionToken = () => _factory.Server.CreateWebSocketClient().ConnectAsync(
            DeviceHubClient.WithQuery(webSocket, DeviceHubDefaults.AccessTokenQueryParameter, profile.Token),
            CancellationToken.None);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await withSessionToken.Should().ThrowAsync<Exception>();
        // The same WebSocket request with a ticket opens.
        using System.Net.WebSockets.WebSocket socket = await _factory.Server.CreateWebSocketClient().ConnectAsync(
            DeviceHubClient.WithQuery(
                webSocket,
                DeviceHubDefaults.AccessTokenQueryParameter,
                await TicketAsync(profile)),
            CancellationToken.None);
        socket.State.Should().Be(System.Net.WebSockets.WebSocketState.Open);
    }

    [Test]
    public async Task Ticket_InTheQueryStringOfTheApi_IsRejected()
    {
        Profile profile = await CreateProfileAsync();

        HttpResponseMessage response = await _anonymous.GetAsync(
            $"/api/v1/me/devices?{DeviceHubDefaults.AccessTokenQueryParameter}={await TicketAsync(profile)}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task Ticket_Expired_IsRejected()
    {
        Profile profile = await CreateProfileAsync();
        // A ticket issued two minutes ago with the key of the server: its 60 seconds are over.
        JwtTokenIssuer past = new(
            _factory.Services.GetRequiredService<SigningKeyStore>(),
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-2)));
        JwtTokenIssuer now = new(_factory.Services.GetRequiredService<SigningKeyStore>(), TimeProvider.System);

        HttpResponseMessage expired = await NegotiateWithHeaderAsync(
            past.IssueDeviceHubTicket(profile.Id, profile.SessionId).Ticket);
        HttpResponseMessage valid = await NegotiateWithHeaderAsync(
            now.IssueDeviceHubTicket(profile.Id, profile.SessionId).Ticket);

        await expired.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Ticket_OfAnEndedSession_IsRejected()
    {
        Profile profile = await CreateProfileAsync();
        string ticket = await TicketAsync(profile);

        (await profile.Client.DeleteAsync("/api/v1/sessions/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage response = await NegotiateWithHeaderAsync(ticket);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task Connection_WithSessionTokenInTheHeader_Works()
    {
        Profile profile = await CreateProfileAsync();
        await using var native = DeviceHubClient.Create(
            _factory.Server,
            () => Task.FromResult<string?>(profile.Token),
            webSocketHeader: true);

        await native.StartAsync();
        RegisterResultDto result = await native.RegisterAsync("native-app", type: DeviceTypeDto.Phone);

        result.Status.Should().Be(RegisterStatusDto.Registered);
    }

    [Test]
    public async Task Connection_OverLongPolling_Works()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient first = await ConnectAsync(profile, HttpTransportType.LongPolling);
        await using DeviceHubClient second = await ConnectAsync(profile, HttpTransportType.LongPolling);
        await first.RegisterAsync("first");

        await second.RegisterAsync("second");

        DeviceListDto devices = await first.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged);
        devices.Devices.Select(device => device.Id).Should().BeEquivalentTo(["first", "second"]);
    }

    [Test]
    public async Task Calls_BeforeRegistering_AnswerNotRegistered()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient client = await ConnectAsync(profile);

        CommandResultDto permission = await client.SetRemoteControlAsync(true);
        PublishResultDto publication = await client.PublishAsync(Playback(), claim: true);
        CommandResultDto command = await client.SendAsync(Command("other", DeviceCommandTypeDto.Play));
        CommandResultDto transfer = await client.TransferAsync("other", play: true);
        DeviceQueueResultDto queue = await client.GetQueueAsync();

        permission.Status.Should().Be(CommandStatusDto.NotRegistered);
        publication.Status.Should().Be(PublishStatusDto.NotRegistered);
        command.Status.Should().Be(CommandStatusDto.NotRegistered);
        transfer.Status.Should().Be(CommandStatusDto.NotRegistered);
        queue.Should().BeEquivalentTo(new { Status = CommandStatusDto.NotRegistered, Queue = (DeviceQueueDto?)null });
    }

    [Test]
    public async Task Register_TwoDevicesOfAProfile_SeeEachOtherLive()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient laptop = await ConnectAsync(profile);
        await using DeviceHubClient phone = await ConnectAsync(profile);
        await laptop.RegisterAsync("laptop", remoteControl: true);

        RegisterResultDto registered = await phone.RegisterAsync("phone", type: DeviceTypeDto.Phone);

        registered.Status.Should().Be(RegisterStatusDto.Registered);
        registered.Errors.Should().BeEmpty();
        registered.Snapshot!.DeviceId.Should().Be("phone");
        registered.Snapshot.Devices.Select(device => device.Id).Should().BeEquivalentTo(["laptop", "phone"]);
        DeviceListDto seen = await laptop.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged);
        seen.Devices.Should().ContainSingle(device => device.Id == "phone")
            .Which.Should().BeEquivalentTo(new { Type = DeviceTypeDto.Phone, Online = true, RemoteControl = false });
        seen.Devices.Should().ContainSingle(device => device.Id == "laptop")
            .Which.RemoteControl.Should().BeTrue();

        await phone.SetRemoteControlAsync(true);
        (await laptop.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Single(device => device.Id == "phone").RemoteControl.Should().BeTrue();

        await phone.DisposeAsync();
        (await laptop.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Single(device => device.Id == "phone").Should().BeEquivalentTo(
                new { Online = false, RemoteControl = false });
    }

    [Test]
    public async Task Register_InvalidDevice_ReturnsCodes()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient client = await ConnectAsync(profile);

        RegisterResultDto result = await client.RegisterAsync("not valid!", name: new string('x', 101));

        result.Status.Should().Be(RegisterStatusDto.Invalid);
        result.Snapshot.Should().BeNull();
        result.Errors.Should().BeEquivalentTo(
        [
            new { Field = "deviceId", Code = ValidationErrorCodeDto.InvalidFormat },
            new { Field = "name", Code = ValidationErrorCodeDto.MaxLength },
        ]);
    }

    [Test]
    public async Task Register_SameDeviceAgain_ReplacesTheOlderConnection()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient older = await ConnectAsync(profile);
        await using DeviceHubClient newer = await ConnectAsync(profile);
        await older.RegisterAsync("browser");

        await newer.RegisterAsync("browser");

        await older.NextAsync<DeviceReplacedDto>(DeviceHubClient.DeviceReplaced);
        (await older.PublishAsync(Playback(), claim: true)).Status.Should().Be(PublishStatusDto.NotRegistered);
        (await newer.PublishAsync(Playback(), claim: true)).Status.Should().Be(PublishStatusDto.Accepted);
    }

    [Test]
    public async Task PublishPlayback_OfTheActiveDevice_ReachesTheRemotes()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");

        PublishResultDto published = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id, _second.Id], 1, 5000),
            claim: true);
        PublishResultDto notActive = await remote.PublishAsync(Playback(LivePlaybackStatusDto.Paused, [_first.Id], 0));

        published.Status.Should().Be(PublishStatusDto.Accepted);
        notActive.Status.Should().Be(PublishStatusDto.NotActive);
        LivePlaybackDto playback = await remote.NextAsync<LivePlaybackDto>(DeviceHubClient.PlaybackChanged);
        playback.Should().BeEquivalentTo(new
        {
            published.Version,
            published.QueueVersion,
            ActiveDeviceId = "speaker",
            Status = LivePlaybackStatusDto.Playing,
            CurrentIndex = 1,
            CurrentTrackId = (Guid?)_second.Id,
            QueueLength = 2,
            Repeat = RepeatModeDto.All,
            Volume = 0.5,
        });
        speaker.HasMessage(DeviceHubClient.PlaybackChanged).Should().BeFalse("the device that published knows it");

        DeviceQueueResultDto queue = await remote.GetQueueAsync();
        queue.Status.Should().Be(CommandStatusDto.Accepted);
        queue.Queue!.QueueVersion.Should().Be(published.QueueVersion);
        queue.Queue.Items.Select(entry => entry.Track!.Title).Should().Equal("First Hub Song", "Second Hub Song");
    }

    [Test]
    public async Task SendCommand_ToTheActiveDeviceWithPermission_IsDelivered()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");
        PublishResultDto published = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0),
            claim: true);

        CommandResultDto result = await remote.SendAsync(
            new DeviceCommandDto
            {
                TargetDeviceId = "speaker",
                Type = DeviceCommandTypeDto.SetVolume,
                Volume = 0.25,
                Muted = false,
            });

        result.Should().BeEquivalentTo(new
        {
            Status = CommandStatusDto.Accepted,
            published.Version,
            published.QueueVersion,
        });
        ReceivedCommandDto received = await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived);
        received.Should().BeEquivalentTo(new
        {
            FromDeviceId = "remote",
            Type = DeviceCommandTypeDto.SetVolume,
            published.Version,
            Volume = (double?)0.25,
            Muted = (bool?)false,
            Tracks = (IReadOnlyList<TrackDto>?)null,
        });
    }

    [Test]
    public async Task SendCommand_PlayTracks_PassesTheSongsOfTheLibraryOn()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");
        await speaker.PublishAsync(Playback(), claim: true);
        Guid albumId = _first.AlbumId!.Value;

        CommandResultDto result = await remote.SendAsync(new DeviceCommandDto
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.PlayTracks,
            TrackIds = [Guid.NewGuid(), _first.Id, _second.Id],
            StartIndex = 1,
            Source = new PlaybackSourceDto { Kind = PlaybackSourceKindDto.Album, Id = albumId },
            Shuffle = false,
        });

        result.Status.Should().Be(CommandStatusDto.Accepted);
        ReceivedCommandDto received = await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived);
        received.Tracks!.Select(track => track.Id).Should().Equal(_first.Id, _second.Id);
        received.Tracks![0].Title.Should().Be("First Hub Song");
        received.StartIndex.Should().Be(0);
        received.Source.Should().BeEquivalentTo(new { Kind = PlaybackSourceKindDto.Album, Id = albumId });
    }

    [Test]
    public async Task SendCommand_ToDeviceWithoutPermission_IsRefusedAndNothingIsDelivered()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: false);
        await remote.RegisterAsync("remote");
        await speaker.PublishAsync(Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0), claim: true);

        CommandResultDto refused = await remote.SendAsync(Command("speaker", DeviceCommandTypeDto.Pause));
        CommandResultDto handover = await remote.TransferAsync("remote", play: true);
        await speaker.SetRemoteControlAsync(true);
        CommandResultDto allowed = await remote.SendAsync(Command("speaker", DeviceCommandTypeDto.Next));

        refused.Status.Should().Be(CommandStatusDto.RemoteControlNotAllowed);
        handover.Status.Should().Be(CommandStatusDto.RemoteControlNotAllowed, "taking playback away is control too");
        allowed.Status.Should().Be(CommandStatusDto.Accepted);
        // Messages of a connection arrive in order: the first command the speaker gets is the allowed one.
        (await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived)).Type
            .Should().Be(DeviceCommandTypeDto.Next);
        speaker.HasMessage(DeviceHubClient.CommandReceived).Should().BeFalse();
    }

    [Test]
    public async Task SendCommand_ToADeviceThatDoesNotPlay_ReturnsNotActive()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient tablet = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await tablet.RegisterAsync("tablet", remoteControl: true);
        await remote.RegisterAsync("remote");
        await speaker.PublishAsync(Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0), claim: true);

        CommandResultDto result = await remote.SendAsync(Command("tablet", DeviceCommandTypeDto.Play));
        CommandResultDto unknown = await remote.SendAsync(Command("nowhere", DeviceCommandTypeDto.Play));

        result.Status.Should().Be(CommandStatusDto.NotActive);
        unknown.Status.Should().Be(CommandStatusDto.DeviceNotFound);
    }

    [Test]
    public async Task SendCommand_WithAStaleQueueVersion_ReturnsConflict()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");
        PublishResultDto first = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id, _second.Id], 0),
            claim: true);
        PublishResultDto changed = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_second.Id, _first.Id, _second.Id], 0));

        CommandResultDto stale = await remote.SendAsync(new DeviceCommandDto
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.RemoveQueueEntry,
            Index = 1,
            ExpectedQueueVersion = first.QueueVersion,
        });
        CommandResultDto current = await remote.SendAsync(new DeviceCommandDto
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.RemoveQueueEntry,
            Index = 1,
            ExpectedQueueVersion = changed.QueueVersion,
        });
        CommandResultDto outside = await remote.SendAsync(new DeviceCommandDto
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.RemoveQueueEntry,
            Index = 3,
            ExpectedQueueVersion = changed.QueueVersion,
        });

        changed.QueueVersion.Should().BeGreaterThan(first.QueueVersion);
        stale.Should().BeEquivalentTo(
            new { Status = CommandStatusDto.Conflict, changed.Version, changed.QueueVersion });
        current.Status.Should().Be(CommandStatusDto.Accepted);
        outside.Status.Should().Be(CommandStatusDto.Invalid);
        outside.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "index", Code = ValidationErrorCodeDto.OutOfRange, Min = 0, Max = 2 });
        (await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived)).Should().BeEquivalentTo(new
        {
            Type = DeviceCommandTypeDto.RemoveQueueEntry,
            Index = (int?)1,
            ExpectedQueueVersion = (long?)changed.QueueVersion,
        });
        speaker.HasMessage(DeviceHubClient.CommandReceived).Should().BeFalse();
    }

    [Test]
    public async Task TransferPlayback_TwoHandOversAtOnce_OneWinsAndTheOtherConflicts()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient phone = await ConnectAsync(profile);
        await using DeviceHubClient tablet = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await phone.RegisterAsync("phone");
        await tablet.RegisterAsync("tablet");
        PublishResultDto published = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0),
            claim: true);

        CommandResultDto[] results = await Task.WhenAll(
            phone.TransferAsync("phone", play: true, published.Version),
            tablet.TransferAsync("tablet", play: true, published.Version));

        results.Select(result => result.Status).Should().BeEquivalentTo(
            [CommandStatusDto.Accepted, CommandStatusDto.Conflict]);
        results.Should().AllSatisfy(result => result.Version.Should().Be(published.Version + 1));
    }

    [Test]
    public async Task TransferPlayback_Always_SavesThePlaybackStateAtTheCurrentPosition()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient phone = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await phone.RegisterAsync("phone", name: "Phone of the test");
        var elapsed = Stopwatch.StartNew();
        await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id, _second.Id], 1, 10_000),
            claim: true);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        CommandResultDto result = await phone.TransferAsync("phone", play: true);
        long upper = 10_000 + elapsed.ElapsedMilliseconds;

        result.Status.Should().Be(CommandStatusDto.Accepted);
        PlaybackStateDto state = await profile.Client.GetJsonAsync<PlaybackStateDto>("/api/v1/me/playback-state");
        state.Items.Select(item => item.Track.Id).Should().Equal(_first.Id, _second.Id);
        state.CurrentIndex.Should().Be(1);
        state.PositionMilliseconds.Should().BeInRange(11_200, (int)upper);
        state.Repeat.Should().Be(RepeatModeDto.All);
        state.Device.Should().BeEquivalentTo(new { Id = "phone", Name = "Phone of the test" });

        LivePlaybackDto atSpeaker = await speaker.NextAsync<LivePlaybackDto>(DeviceHubClient.PlaybackChanged);
        LivePlaybackDto atPhone = await phone.NextMatchingAsync<LivePlaybackDto>(
            DeviceHubClient.PlaybackChanged,
            playback => playback.Handover is not null);
        foreach (LivePlaybackDto playback in new[] { atSpeaker, atPhone })
        {
            playback.Should().BeEquivalentTo(new
            {
                ActiveDeviceId = "phone",
                Status = LivePlaybackStatusDto.Paused,
                Handover = new { FromDeviceId = "speaker", Play = true },
            });
        }
    }

    [Test]
    public async Task Payload_LargerThanTheLimit_ClosesTheConnection()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient client = await ConnectAsync(profile);
        await client.RegisterAsync("device");

        Func<Task> tooLarge = () => client.SendAsync(Command(new string('a', 300 * 1024), DeviceCommandTypeDto.Play));

        await tooLarge.Should().ThrowAsync<Exception>();
        await client.WaitClosedAsync();
    }

    [Test]
    public async Task Payload_WithTooManySongsOrValuesOutOfRange_IsInvalidWithCodes()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");
        await speaker.PublishAsync(Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0), claim: true);

        CommandResultDto songs = await remote.SendAsync(new DeviceCommandDto
        {
            TargetDeviceId = "speaker",
            Type = DeviceCommandTypeDto.AddToQueue,
            TrackIds = [.. Enumerable.Range(0, 2001).Select(_ => Guid.NewGuid())],
        });
        CommandResultDto volume = await remote.SendAsync(
            new DeviceCommandDto { TargetDeviceId = "speaker", Type = DeviceCommandTypeDto.SetVolume, Volume = 1.5 });
        PublishResultDto queue = await speaker.PublishAsync(Playback(
            LivePlaybackStatusDto.Playing,
            [.. Enumerable.Range(0, 2001).Select(_ => _first.Id)],
            0));
        PublishResultDto position = await speaker.PublishAsync(
            Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0, -5));
        PublishResultDto source = await speaker.PublishAsync(
            Playback(
                LivePlaybackStatusDto.Playing,
                [_first.Id],
                0,
                source: new PlaybackSourceDto { Kind = null, Id = Guid.NewGuid() }));

        songs.Errors.Should().BeEquivalentTo(
            [new { Field = "trackIds", Code = ValidationErrorCodeDto.MaxItems, Max = 2000 }]);
        volume.Errors.Should().BeEquivalentTo(
            [new { Field = "volume", Code = ValidationErrorCodeDto.OutOfRange, Min = 0, Max = 1 }]);
        queue.Errors.Should().BeEquivalentTo(
            [new { Field = "playback.queue.items", Code = ValidationErrorCodeDto.MaxItems, Max = 2000 }]);
        position.Errors.Should().BeEquivalentTo(
            [new { Field = "playback.positionMilliseconds", Code = ValidationErrorCodeDto.OutOfRange }]);
        source.Errors.Should().BeEquivalentTo(
            [new { Field = "playback.queue.source.kind", Code = ValidationErrorCodeDto.Required }]);
        new[] { songs.Status, volume.Status }.Should().AllBeEquivalentTo(CommandStatusDto.Invalid);
        new[] { queue.Status, position.Status, source.Status }.Should().AllBeEquivalentTo(PublishStatusDto.Invalid);
        speaker.HasMessage(DeviceHubClient.CommandReceived).Should().BeFalse();
    }

    [Test]
    public async Task Payload_ThatDoesNotFitTheSchema_FailsTheCallAndChangesNothing()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(profile);
        await using DeviceHubClient remote = await ConnectAsync(profile);
        await speaker.RegisterAsync("speaker", remoteControl: true);
        await remote.RegisterAsync("remote");
        await speaker.PublishAsync(Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0), claim: true);

        Func<Task> unknownEnum = () => remote.Connection.InvokeAsync<CommandResultDto>(
            "SendCommand",
            new JsonObject { ["targetDeviceId"] = "speaker", ["type"] = "Explode" });
        Func<Task> numberForEnum = () => remote.Connection.InvokeAsync<CommandResultDto>(
            "SendCommand",
            new JsonObject { ["targetDeviceId"] = "speaker", ["type"] = 1 });
        Func<Task> stringForNumber = () => remote.Connection.InvokeAsync<CommandResultDto>(
            "SendCommand",
            new JsonObject { ["targetDeviceId"] = "speaker", ["type"] = "Seek", ["positionMilliseconds"] = "1000" });
        Func<Task> missingMember = () => remote.Connection.InvokeAsync<CommandResultDto>(
            "SendCommand",
            new JsonObject { ["targetDeviceId"] = "speaker" });

        await unknownEnum.Should().ThrowAsync<HubException>();
        await numberForEnum.Should().ThrowAsync<HubException>();
        await stringForNumber.Should().ThrowAsync<HubException>();
        await missingMember.Should().ThrowAsync<HubException>();
        // The connection stays usable, and only the valid command arrives.
        (await remote.SendAsync(Command("speaker", DeviceCommandTypeDto.Stop))).Status.Should().Be(
            CommandStatusDto.Accepted);
        (await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived)).Type
            .Should().Be(DeviceCommandTypeDto.Stop);
    }

    [Test]
    public async Task Profiles_NeverSeeOrControlTheDevicesOfAnotherProfile()
    {
        Profile owner = await CreateProfileAsync();
        Profile stranger = await CreateProfileAsync();
        await using DeviceHubClient speaker = await ConnectAsync(owner);
        await using DeviceHubClient ownerRemote = await ConnectAsync(owner);
        await using DeviceHubClient strangerDevice = await ConnectAsync(stranger);
        await using DeviceHubClient strangerRemote = await ConnectAsync(stranger);
        await speaker.RegisterAsync("shared-id", remoteControl: true);
        await ownerRemote.RegisterAsync("owner-remote", remoteControl: true);
        // The same device id in another profile is another device.
        RegisterResultDto strangerRegistered = await strangerDevice.RegisterAsync("shared-id");
        await speaker.PublishAsync(Playback(LivePlaybackStatusDto.Playing, [_first.Id], 0), claim: true);

        RegisterResultDto strangerRemoteRegistered = await strangerRemote.RegisterAsync("stranger-remote");
        CommandResultDto toOwnerRemote = await strangerRemote.SendAsync(
            Command("owner-remote", DeviceCommandTypeDto.Pause));
        CommandResultDto toSharedId = await strangerRemote.SendAsync(Command("shared-id", DeviceCommandTypeDto.Pause));
        CommandResultDto handover = await strangerRemote.TransferAsync("owner-remote", play: true);
        DeviceQueueResultDto queue = await strangerRemote.GetQueueAsync();

        strangerRegistered.Snapshot!.Devices.Select(device => device.Id).Should().Equal("shared-id");
        strangerRegistered.Snapshot.Playback.ActiveDeviceId.Should().BeNull();
        strangerRemoteRegistered.Snapshot!.Devices.Select(device => device.Id)
            .Should().BeEquivalentTo(["shared-id", "stranger-remote"]);
        strangerRemoteRegistered.Snapshot.Playback.QueueLength.Should().Be(0);
        toOwnerRemote.Status.Should().Be(CommandStatusDto.DeviceNotFound);
        toSharedId.Status.Should().Be(CommandStatusDto.RemoteControlNotAllowed, "it is the device of the stranger");
        handover.Status.Should().Be(CommandStatusDto.DeviceNotFound);
        queue.Queue!.Items.Should().BeEmpty();

        // The stranger's devices only ever heard of their own profile.
        (await strangerDevice.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Select(device => device.Id).Should().BeEquivalentTo(["shared-id", "stranger-remote"]);
        strangerDevice.HasMessage(DeviceHubClient.DevicesChanged).Should().BeFalse();
        strangerDevice.HasMessage(DeviceHubClient.PlaybackChanged).Should().BeFalse();
        // The first command the owner's speaker gets is the one of its own profile.
        await ownerRemote.SendAsync(Command("shared-id", DeviceCommandTypeDto.Next));
        (await speaker.NextAsync<ReceivedCommandDto>(DeviceHubClient.CommandReceived)).FromDeviceId
            .Should().Be("owner-remote");
        // The owner's devices never heard of the stranger's devices.
        (await ownerRemote.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Select(device => device.Id).Should().BeEquivalentTo(["shared-id", "owner-remote"]);
    }

    [Test]
    public async Task RenamingAndForgettingThroughTheApi_Always_NotifyTheOpenDevices()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient laptop = await ConnectAsync(profile);
        await using (DeviceHubClient old = await ConnectAsync(profile))
        {
            await old.RegisterAsync("old");
        }

        await laptop.RegisterAsync("laptop");

        (await profile.Client.PatchAsJsonAsync("/api/v1/me/devices/laptop", new { name = "Desk" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await laptop.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Single(device => device.Id == "laptop").Name.Should().Be("Desk");
        (await profile.Client.DeleteAsync("/api/v1/me/devices/old")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await laptop.NextAsync<DeviceListDto>(DeviceHubClient.DevicesChanged)).Devices
            .Select(device => device.Id).Should().Equal("laptop");
    }

    [Test]
    public async Task EndingTheSession_Always_ClosesItsConnectionsAtOnce()
    {
        Profile profile = await CreateProfileAsync();
        Profile other = await CreateProfileAsync();
        await using DeviceHubClient registered = await ConnectAsync(profile);
        await using DeviceHubClient unregistered = await ConnectAsync(profile);
        await using DeviceHubClient otherDevice = await ConnectAsync(other);
        await registered.RegisterAsync("phone");
        await otherDevice.RegisterAsync("phone");

        (await profile.Client.DeleteAsync("/api/v1/sessions/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await registered.WaitClosedAsync();
        await unregistered.WaitClosedAsync();
        Func<Task> call = () => registered.GetQueueAsync();
        await call.Should().ThrowAsync<Exception>();
        (await otherDevice.GetQueueAsync()).Status.Should().Be(CommandStatusDto.Accepted);
    }

    [Test]
    public async Task DeletingTheProfile_Always_ClosesItsConnectionsAtOnce()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient device = await ConnectAsync(profile);
        await device.RegisterAsync("phone");

        (await _admin.DeleteAsync($"/api/v1/users/{profile.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await device.WaitClosedAsync();
    }

    [Test]
    public async Task SessionEndedElsewhere_NextCall_FailsAndClosesTheConnection()
    {
        Profile profile = await CreateProfileAsync();
        CreatedSessionDto second = await _anonymous.CreateSessionAsync(profile.Id);
        using HttpClient secondClient = _factory.CreateAuthorizedClient(second.Token);
        await using DeviceHubClient device = await ConnectAsync(profile);
        await device.RegisterAsync("phone");

        // Setting a PIN ends every other session of the profile.
        (await secondClient.PutAsJsonAsync("/api/v1/me/pin", new { pin = "4321" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        Func<Task> call = () => device.GetQueueAsync();

        await call.Should().ThrowAsync<Exception>();
        await device.WaitClosedAsync();
    }

    [Test]
    public async Task Sweep_SessionsThatEnded_AreClosed()
    {
        Profile profile = await CreateProfileAsync();
        await using DeviceHubClient device = await ConnectAsync(profile);
        await device.RegisterAsync("phone");
        await using (AsyncServiceScope scope = _factory.Services.CreateAsyncScope())
        {
            // Ends the session without the API, as an expired session does.
            await scope.ServiceProvider.GetRequiredService<ISessionService>().DeleteAsync(profile.SessionId);
        }

        int ended = await _factory.Services.GetRequiredService<DeviceHubSessionCloser>().CloseEndedSessionsAsync();

        ended.Should().BeGreaterThanOrEqualTo(1);
        await device.WaitClosedAsync();
    }

    private async Task<DeviceHubClient> ConnectAsync(
        Profile profile,
        HttpTransportType transport = HttpTransportType.WebSockets)
    {
        // Like the web app: a new ticket for every request that needs one.
        var client = DeviceHubClient.Create(
            _factory.Server,
            async () => (string?)await TicketAsync(profile),
            transport);
        await client.StartAsync();
        return client;
    }

    private static async Task<string> TicketAsync(Profile profile)
    {
        HttpResponseMessage response = await profile.Client.PostAsync(TicketPath, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DeviceHubTicketDto>(HttpClientExtensions.JsonOptions))!.Ticket;
    }

    private Task<HttpResponseMessage> NegotiateWithHeaderAsync(string? token)
    {
        HttpRequestMessage request = new(HttpMethod.Post, NegotiatePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _anonymous.SendAsync(request);
    }

    private static DeviceCommandDto Command(string target, DeviceCommandTypeDto type)
    {
        return new DeviceCommandDto { TargetDeviceId = target, Type = type };
    }

    private static LivePlaybackInputDto Playback(
        LivePlaybackStatusDto status = LivePlaybackStatusDto.Idle,
        IReadOnlyList<Guid>? trackIds = null,
        int currentIndex = -1,
        int position = 0,
        PlaybackSourceDto? source = null)
    {
        return new LivePlaybackInputDto
        {
            Status = status,
            Queue = new LiveQueueInputDto
            {
                Items = [.. (trackIds ?? []).Select(id => new LiveQueueItemDto { TrackId = id, Autoplay = false })],
                Shuffle = false,
                Source = source,
            },
            CurrentIndex = currentIndex,
            PositionMilliseconds = position,
            DurationMilliseconds = 180_000,
            Repeat = RepeatModeDto.All,
            Volume = 0.5,
            Muted = false,
            Crossfade = false,
            Autoplay = false,
        };
    }

    private async Task<Profile> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Hub {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return new Profile(user.Id, session.Session.Id, session.Token, _factory.CreateAuthorizedClient(session.Token));
    }

    private sealed record Profile(Guid Id, Guid SessionId, string Token, HttpClient Client);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
