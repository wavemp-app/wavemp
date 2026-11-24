using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Data;
using WaveMP.Service.Tests.IntegrationTests.Fakes;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Http;
using WaveMP.Testing.Logs;
using OnlineMetadataExtensions = WaveMP.Core.OnlineMetadata.Extensions.ServiceCollectionExtensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Hosts WaveMP in memory through the real startup in <c>Program.cs</c>, including the validation of the database
/// configuration, the migrations and the creation of the built-in admin profile, against a real PostgreSQL instance
/// (local: docker-compose.dev.yml, CI: service container). Uses its own database, by default <c>wavemp_tests</c>.
/// The connection settings come from <see cref="TestDatabaseSettings"/>. The media folder is an empty temporary
/// folder unless the test passes its own, the data folder (with the cache) is a temporary folder of its own, and no
/// periodic scan runs during the tests. <c>Program</c> is public without a declaration of its own: since .NET 10,
/// ASP.NET Core generates it for apps with top-level statements.
/// </summary>
/// <param name="databaseName">
/// Name of the database. If <c>null</c>, <c>DB_NAME</c> from the environment or <see cref="TestDatabaseName"/>.
/// </param>
/// <param name="freshDatabase">
/// Drops the database before the start, so the service starts like a new installation.
/// </param>
/// <param name="mediaPath">
/// The media folder (<c>MEDIA_PATH</c>); if <c>null</c>, an empty temporary folder that is deleted afterwards.
/// </param>
/// <param name="exporter">
/// Replaces pg_dump for the database backups; if <c>null</c>, a <see cref="FakeDatabaseExporter"/> that succeeds.
/// </param>
/// <param name="settings">
/// Further configuration values of the service, e.g. <c>ADMIN_PIN</c>. Without it, <c>ADMIN_PIN</c> is empty, so a
/// variable of the environment of the test run does not protect the admin profile of the tests.
/// </param>
/// <param name="lrclib">
/// Replaces the client of the online lyrics service LRCLIB, so no test sends requests to it; if <c>null</c>, a
/// substitute that finds nothing.
/// </param>
/// <param name="lastFm">
/// Replaces the client of Last.fm, so no test sends requests to it; if <c>null</c>, a substitute that cannot be
/// reached.
/// </param>
/// <param name="listenBrainz">
/// Replaces the client of ListenBrainz, so no test sends requests to it; if <c>null</c>, a substitute that cannot be
/// reached.
/// </param>
/// <param name="musicBrainz">
/// Replaces the client of MusicBrainz, so no test sends requests to it; if <c>null</c>, a substitute that cannot be
/// reached.
/// </param>
/// <param name="coverArtArchive">
/// Replaces the client of the Cover Art Archive, so no test sends requests to it; if <c>null</c>, a substitute that
/// cannot be reached.
/// </param>
public sealed class WaveMpWebApplicationFactory(
    string? databaseName = null,
    bool freshDatabase = false,
    string? mediaPath = null,
    FakeDatabaseExporter? exporter = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    ILrclibClient? lrclib = null,
    ILastFmClient? lastFm = null,
    IListenBrainzClient? listenBrainz = null,
    IMusicBrainzClient? musicBrainz = null,
    ICoverArtArchiveClient? coverArtArchive = null) : WebApplicationFactory<Program>
{
    public const string TestDatabaseName = "wavemp_tests";

    private const string EnvironmentName = "Testing";

    private readonly TemporaryDirectory? _emptyMedia = mediaPath is null ? new TemporaryDirectory() : null;
    private readonly TemporaryDirectory _data = new();

    /// <summary>
    /// The data folder (<c>DATA_PATH</c>) of the service; the cache lies in its folder <c>cache</c>, the exports of the
    /// database in its folder <c>backups</c>.
    /// </summary>
    public string DataPath => _data.Path;

    /// <summary>
    /// The export of the database that replaces pg_dump.
    /// </summary>
    public FakeDatabaseExporter Exporter { get; } = exporter ?? new FakeDatabaseExporter();

    /// <summary>
    /// The simulated online lyrics service LRCLIB.
    /// </summary>
    public ILrclibClient Lrclib { get; } = lrclib ?? CreateLrclib();

    /// <summary>
    /// The simulated Last.fm.
    /// </summary>
    public ILastFmClient LastFm { get; } = lastFm ?? CreateLastFm();

    /// <summary>
    /// The simulated ListenBrainz.
    /// </summary>
    public IListenBrainzClient ListenBrainz { get; } = listenBrainz ?? CreateListenBrainz();

    /// <summary>
    /// The simulated MusicBrainz.
    /// </summary>
    public IMusicBrainzClient MusicBrainz { get; } = musicBrainz ?? CreateMusicBrainz();

    /// <summary>
    /// The simulated Cover Art Archive.
    /// </summary>
    public ICoverArtArchiveClient CoverArtArchive { get; } = coverArtArchive ?? CreateCoverArtArchive();

    /// <summary>
    /// The simulated web services of the online lookup of artist pictures (MusicBrainz, Wikidata, Wikimedia Commons,
    /// fanart.tv): it replaces the HTTP handler of the lookup, so no test sends a request to them. Answers 404 to every
    /// request until a test registers answers, and records every request.
    /// </summary>
    public SimulatedWebHandler ArtistLookups { get; } = new();

    /// <summary>
    /// Every entry the service logged: of WaveMP itself of every level, of the frameworks from the configured level.
    /// </summary>
    public LogMessageCollector Log { get; } = new();

    /// <summary>
    /// The errors the service logged.
    /// </summary>
    public ErrorLogCollector ErrorLog { get; } = new();

    /// <summary>
    /// Connection string of the database of the service, for tests that change the database next to the service.
    /// </summary>
    public string ConnectionString => TestDatabaseSettings.BuildConnectionString(DatabaseName);

    private string DatabaseName => databaseName ?? Environment.GetEnvironmentVariable("DB_NAME") ?? TestDatabaseName;

    /// <summary>
    /// Drops the database of the running service, e.g. to test how the service behaves without it or to clean up.
    /// Only for factories created with a database name of their own.
    /// </summary>
    public async Task DeleteDatabaseAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WaveMpDbContext>().Database.EnsureDeletedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        Dictionary<string, string?> values = TestDatabaseSettings.ToConfiguration(DatabaseName);
        values["MEDIA_PATH"] = mediaPath ?? _emptyMedia!.Path;
        values["DATA_PATH"] = _data.Path;
        // The periodic scan would start one minute after the start; tests start scans themselves.
        values["Scanning:StartupDelay"] = "1.00:00:00";
        // Short HLS segments, so the synthetic videos of a few seconds have several.
        values["Transcoding:SegmentDuration"] = "00:00:02";
        // The PIN of the admin profile only comes from the test itself.
        values["ADMIN_PIN"] = string.Empty;
        if (Environment.GetEnvironmentVariable("FFMPEG_PATH") is { Length: > 0 } ffmpeg)
        {
            string extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            values["Metadata:FfprobePath"] = Path.Combine(ffmpeg, "ffprobe" + extension);
            values["Caching:FfmpegPath"] = Path.Combine(ffmpeg, "ffmpeg" + extension);
            values["Transcoding:FfmpegPath"] = Path.Combine(ffmpeg, "ffmpeg" + extension);
        }

        foreach ((string key, string? value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(values));
        builder.ConfigureLogging(logging => logging
            .AddProvider(ErrorLog)
            .AddProvider(Log)
            .AddFilter<LogMessageCollector>("WaveMP", LogLevel.Trace));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseExporter>();
            services.AddSingleton<IDatabaseExporter>(Exporter);
            services.RemoveAll<ILrclibClient>();
            services.AddSingleton(Lrclib);
            services.RemoveAll<ILastFmClient>();
            services.AddSingleton(LastFm);
            services.RemoveAll<IListenBrainzClient>();
            services.AddSingleton(ListenBrainz);
            services.RemoveAll<IMusicBrainzClient>();
            services.AddSingleton(MusicBrainz);
            services.RemoveAll<ICoverArtArchiveClient>();
            services.AddSingleton(CoverArtArchive);
            services.RemoveAllKeyed<HttpMessageHandler>(OnlineMetadataExtensions.ArtistLookupHandlerKey);
            services.AddKeyedSingleton<HttpMessageHandler>(
                OnlineMetadataExtensions.ArtistLookupHandlerKey,
                ArtistLookups);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (freshDatabase)
        {
            TestDatabaseSettings.DropDatabase(DatabaseName);
        }

        IHost host = base.CreateHost(builder);

        // Program.cs exits without starting the service if the database or PIN configuration is invalid or the backup
        // before the migrations failed.
        if (!host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested)
        {
            host.Dispose();
            throw new InvalidOperationException(
                "WaveMP did not start because the database or PIN configuration is invalid or the backup before the "
                + "migrations failed. Check DB_PASSWORD and DB_PORT in .env or in the environment.");
        }

        return host;
    }

    /// <summary>
    /// A Last.fm that cannot be reached.
    /// </summary>
    public static ILastFmClient CreateLastFm()
    {
        ILastFmClient client = Substitute.For<ILastFmClient>();
        client.GetSessionAsync(default!, default).ReturnsForAnyArgs(ScrobbleAuthorization.Unavailable);
        client.UpdateNowPlayingAsync(default!, default!, default).ReturnsForAnyArgs(ScrobbleSendStatus.Unavailable);
        client.ScrobbleAsync(default!, default!, default).ReturnsForAnyArgs(ScrobbleSendStatus.Unavailable);
        return client;
    }

    /// <summary>
    /// A ListenBrainz that cannot be reached.
    /// </summary>
    public static IListenBrainzClient CreateListenBrainz()
    {
        IListenBrainzClient client = Substitute.For<IListenBrainzClient>();
        client.ValidateTokenAsync(default!, default).ReturnsForAnyArgs(ScrobbleAuthorization.Unavailable);
        client.SubmitPlayingNowAsync(default!, default!, default)
            .ReturnsForAnyArgs(ScrobbleSendStatus.Unavailable);
        client.SubmitListensAsync(default!, default!, default).ReturnsForAnyArgs(ScrobbleSendStatus.Unavailable);
        return client;
    }

    /// <summary>
    /// A MusicBrainz that cannot be reached.
    /// </summary>
    public static IMusicBrainzClient CreateMusicBrainz()
    {
        IMusicBrainzClient client = Substitute.For<IMusicBrainzClient>();
        client.SearchRecordingsAsync(default!, default)
            .ReturnsForAnyArgs(OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Failed(OnlineStatus.Unavailable));
        client.SearchReleasesAsync(default!, default)
            .ReturnsForAnyArgs(OnlineResponse<IReadOnlyList<MusicBrainzRelease>>.Failed(OnlineStatus.Unavailable));
        return client;
    }

    /// <summary>
    /// A Cover Art Archive that cannot be reached.
    /// </summary>
    public static ICoverArtArchiveClient CreateCoverArtArchive()
    {
        ICoverArtArchiveClient client = Substitute.For<ICoverArtArchiveClient>();
        client.GetFrontCoverAsync(default, default, default)
            .ReturnsForAnyArgs(OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable));
        return client;
    }

    private static ILrclibClient CreateLrclib()
    {
        ILrclibClient client = Substitute.For<ILrclibClient>();
        client.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        client.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _emptyMedia?.Dispose();
        _data.Dispose();
    }
}
