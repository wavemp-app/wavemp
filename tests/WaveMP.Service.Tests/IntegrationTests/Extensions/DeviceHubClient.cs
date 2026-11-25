using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using WaveMP.Service.Dtos;
using WaveMP.Service.Hubs;

namespace WaveMP.Service.Tests.IntegrationTests.Extensions;

/// <summary>
/// A client of the real-time connection of the devices for the integration tests: the official SignalR client
/// against the in-memory server, which keeps every message of the server per target until a test reads it.
/// </summary>
internal sealed class DeviceHubClient : IAsyncDisposable
{
    public const string DevicesChanged = nameof(IDeviceHubClient.DevicesChanged);
    public const string PlaybackChanged = nameof(IDeviceHubClient.PlaybackChanged);
    public const string CommandReceived = nameof(IDeviceHubClient.CommandReceived);
    public const string DeviceReplaced = nameof(IDeviceHubClient.DeviceReplaced);

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, Channel<object>> _messages = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<Exception?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DeviceHubClient(HubConnection connection)
    {
        Connection = connection;
        Listen<DeviceListDto>(DevicesChanged);
        Listen<LivePlaybackDto>(PlaybackChanged);
        Listen<ReceivedCommandDto>(CommandReceived);
        Listen<DeviceReplacedDto>(DeviceReplaced);
        connection.Closed += exception =>
        {
            _closed.TrySetResult(exception);
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    /// <summary>
    /// Completes when the connection closed, with the error it closed with.
    /// </summary>
    public Task<Exception?> Closed => _closed.Task;

    /// <summary>
    /// Builds a connection to the hub of the server. <paramref name="credentials"/> is asked for every request, like
    /// the access token factory of the web app: negotiate and long polling send it in the header Authorization,
    /// WebSockets in the query parameter <c>access_token</c> (as browsers do), unless
    /// <paramref name="webSocketHeader"/> is set.
    /// </summary>
    public static DeviceHubClient Create(
        TestServer server,
        Func<Task<string?>> credentials,
        HttpTransportType transport = HttpTransportType.WebSockets,
        bool webSocketHeader = false)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, DeviceHubDefaults.Path), transport, options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = credentials;
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    WebSocketClient client = server.CreateWebSocketClient();
                    Uri uri = context.Uri;
                    string? token = await credentials();
                    if (token is not null && webSocketHeader)
                    {
                        client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
                    }
                    else if (token is not null)
                    {
                        uri = WithQuery(uri, DeviceHubDefaults.AccessTokenQueryParameter, token);
                    }

                    return await client.ConnectAsync(uri, cancellationToken);
                };
            })
            .Build();
        return new DeviceHubClient(connection);
    }

    /// <summary>
    /// Adds a query parameter to an address.
    /// </summary>
    public static Uri WithQuery(Uri uri, string name, string value)
    {
        string query = uri.Query.TrimStart('?');
        return new UriBuilder(uri)
        {
            Query = (query.Length > 0 ? query + "&" : string.Empty) + $"{name}={Uri.EscapeDataString(value)}",
        }.Uri;
    }

    public Task StartAsync()
    {
        return Connection.StartAsync();
    }

    public Task<RegisterResultDto> RegisterAsync(
        string deviceId,
        bool remoteControl = false,
        DeviceTypeDto type = DeviceTypeDto.Desktop,
        string? name = null)
    {
        return Connection.InvokeAsync<RegisterResultDto>(
            "Register",
            new RegisterDeviceDto
            {
                DeviceId = deviceId,
                Name = name ?? $"Device {deviceId}",
                Type = type,
                RemoteControl = remoteControl,
            });
    }

    public Task<CommandResultDto> SetRemoteControlAsync(bool allowed)
    {
        return Connection.InvokeAsync<CommandResultDto>(
            "SetRemoteControl",
            new SetRemoteControlDto { Allowed = allowed });
    }

    public Task<PublishResultDto> PublishAsync(LivePlaybackInputDto playback, bool claim = false)
    {
        return Connection.InvokeAsync<PublishResultDto>(
            "PublishPlayback",
            new PublishPlaybackDto { Claim = claim, Playback = playback });
    }

    public Task<CommandResultDto> SendAsync(DeviceCommandDto command)
    {
        return Connection.InvokeAsync<CommandResultDto>("SendCommand", command);
    }

    public Task<CommandResultDto> TransferAsync(string targetDeviceId, bool play, long? expectedVersion = null)
    {
        return Connection.InvokeAsync<CommandResultDto>(
            "TransferPlayback",
            new TransferPlaybackDto { TargetDeviceId = targetDeviceId, Play = play, ExpectedVersion = expectedVersion });
    }

    public Task<DeviceQueueResultDto> GetQueueAsync()
    {
        return Connection.InvokeAsync<DeviceQueueResultDto>("GetQueue");
    }

    /// <summary>
    /// The next message of the server to <paramref name="target"/> that has not been read yet.
    /// </summary>
    public async Task<T> NextAsync<T>(string target)
    {
        using CancellationTokenSource timeout = new(_timeout);
        return (T)await _messages[target].Reader.ReadAsync(timeout.Token);
    }

    /// <summary>
    /// Reads the messages to <paramref name="target"/> until one matches.
    /// </summary>
    public async Task<T> NextMatchingAsync<T>(string target, Func<T, bool> predicate)
    {
        while (true)
        {
            T message = await NextAsync<T>(target);
            if (predicate(message))
            {
                return message;
            }
        }
    }

    /// <summary>
    /// Whether a message to <paramref name="target"/> arrived that has not been read yet.
    /// </summary>
    public bool HasMessage(string target)
    {
        return _messages[target].Reader.TryPeek(out _);
    }

    /// <summary>
    /// Waits until the connection closed.
    /// </summary>
    public Task<Exception?> WaitClosedAsync()
    {
        return Closed.WaitAsync(_timeout);
    }

    public ValueTask DisposeAsync()
    {
        return Connection.DisposeAsync();
    }

    private void Listen<T>(string target)
    {
        var channel = Channel.CreateUnbounded<object>();
        _messages[target] = channel;
        Connection.On<T>(target, message => channel.Writer.TryWrite(message!));
    }
}
