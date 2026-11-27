using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http.Connections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Hubs;
using WaveMP.Service.Tests.IntegrationTests.Extensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Browsers put the ticket of the real-time connection of the devices into the query string, which the request log of
/// ASP.NET Core writes out in full. With every log category raised to Trace, as an operator might do, neither a ticket
/// nor a session token reaches the log.
/// </summary>
[TestFixture]
[Category("Integration")]
public class DeviceHubLogTests
{
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_device_hub_log",
            freshDatabase: true,
            settings: new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Trace",
                ["Logging:LogLevel:Microsoft"] = "Trace",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace",
                ["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Trace",
                // The SQL of the database is not about the connection and only makes the log long.
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
                // A rule of the collecting provider itself takes precedence over the rules for all providers.
                ["Logging:WaveMP.Testing.Logs.LogMessageCollector:LogLevel:Default"] = "Trace",
                ["Logging:WaveMP.Testing.Logs.LogMessageCollector:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] =
                    "Trace",
            });
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [TestCase(HttpTransportType.WebSockets)]
    [TestCase(HttpTransportType.LongPolling)]
    public async Task Connection_WithTickets_NeverLogsTicketsOrSessionTokens(HttpTransportType transport)
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Log {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        using HttpClient client = _factory.CreateAuthorizedClient(session.Token);
        ConcurrentBag<string> tickets = [];
        int before = _factory.Log.Entries.Count;

        await using (var device = DeviceHubClient.Create(
            _factory.Server,
            async () =>
            {
                HttpResponseMessage response = await client.PostAsync("/api/v1/me/device-hub-tickets", null);
                string ticket = (await response.Content.ReadFromJsonAsync<DeviceHubTicketDto>(
                    HttpClientExtensions.JsonOptions))!.Ticket;
                tickets.Add(ticket);
                return ticket;
            },
            transport))
        {
            await device.StartAsync();
            (await device.RegisterAsync("logged")).Status.Should().Be(RegisterStatusDto.Registered);
            (await device.GetQueueAsync()).Status.Should().Be(CommandStatusDto.Accepted);
        }

        // A session token in the query string is rejected, and not logged either.
        HttpResponseMessage rejected = await _anonymous.PostAsync(
            $"{DeviceHubDefaults.Path}/negotiate?negotiateVersion=1&access_token={session.Token}",
            null);

        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tickets.Should().NotBeEmpty();
        string[] entries = [.. _factory.Log.Entries.Skip(before)];
        entries.Should().Contain(
            entry => entry.Contains("Microsoft.AspNetCore.Http.Connections", StringComparison.Ordinal)
                || entry.Contains("Microsoft.AspNetCore.SignalR", StringComparison.Ordinal),
            "the connection logs at Trace in this test");
        foreach (string secret in tickets.Append(session.Token))
        {
            string signature = secret[(secret.LastIndexOf('.') + 1)..];
            entries.Should().NotContain(entry => entry.Contains(signature, StringComparison.Ordinal));
        }

        entries.Should().NotContain(entry => entry.Contains("access_token=", StringComparison.Ordinal));
    }
}
