using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Core.Scrobbling.Credentials;
using WaveMP.Core.Scrobbling.Extensions;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Core.Streaming.Histories;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Tests.Extensions;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddWaveMpScrobbling_WithItsDependencies_ResolvesAllServices()
    {
        using ServiceProvider provider = CreateServices([]).BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IScrobbleConnectionService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IScrobbleQueue>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IScrobbleQueueProcessor>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<INowPlayingService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ILastFmClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IListenBrainzClient>().Should().NotBeNull();
        scope.ServiceProvider.GetServices<IPlayHistoryListener>().Should().ContainSingle();
        provider.GetServices<IHostedService>().Should().ContainSingle();
    }

    [Test]
    public void AddWaveMpScrobbling_WithoutSettings_UsesTheDefaultsWithoutLastFm()
    {
        using ServiceProvider provider = CreateServices([]).BuildServiceProvider();

        ScrobblingOptions options = provider.GetRequiredService<IOptions<ScrobblingOptions>>().Value;

        options.IsLastFmConfigured.Should().BeFalse();
        options.BatchSize.Should().Be(50);
        options.MaxAge.Should().Be(TimeSpan.FromDays(14));
        options.ListenBrainzApiUrl.Should().Be(new Uri("https://api.listenbrainz.org/"));
    }

    [Test]
    public void AddWaveMpScrobbling_WithLastFmVariables_ConfiguresLastFm()
    {
        using ServiceProvider provider = CreateServices(new()
        {
            [ScrobblingOptions.LastFmApiKeyVariable] = " key ",
            [ScrobblingOptions.LastFmSharedSecretVariable] = "secret",
            ["Scrobbling:RetryDelay"] = "00:00:05",
        }).BuildServiceProvider();

        ScrobblingOptions options = provider.GetRequiredService<IOptions<ScrobblingOptions>>().Value;

        options.LastFmApiKey.Should().Be("key");
        options.LastFmSharedSecret.Should().Be("secret");
        options.IsLastFmConfigured.Should().BeTrue();
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void CredentialProtector_ProtectedCredential_IsNotThePlainTextAndCanBeRead()
    {
        using ServiceProvider provider = CreateServices([]).BuildServiceProvider();
        IScrobbleCredentialProtector protector = provider.GetRequiredService<IScrobbleCredentialProtector>();

        string protectedCredential = protector.Protect("session-key");

        protectedCredential.Should().NotContain("session-key");
        protector.Unprotect(protectedCredential).Should().Be("session-key");
        protector.Unprotect("not protected").Should().BeNull();
    }

    private static ServiceCollection CreateServices(Dictionary<string, string?> settings)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new ReversingProtector());
        services.AddScoped(_ => Substitute.For<IScrobbleRepository>());
        services.AddScoped(_ => Substitute.For<ITrackRepository>());
        services.AddWaveMpScrobbling();
        return services;
    }

    /// <summary>
    /// A stand-in for Data Protection that reverses the bytes and fails for data it did not protect.
    /// </summary>
    private sealed class ReversingProtector : IDataProtector
    {
        private static readonly byte[] _marker = [0x57, 0x4d, 0x50];

        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        public byte[] Protect(byte[] plaintext)
        {
            return [.. _marker, .. plaintext.Reverse()];
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            if (protectedData.Length < _marker.Length
                || !protectedData.AsSpan(0, _marker.Length).SequenceEqual(_marker))
            {
                throw new System.Security.Cryptography.CryptographicException("Not protected.");
            }

            return [.. protectedData.Skip(_marker.Length).Reverse()];
        }
    }
}
