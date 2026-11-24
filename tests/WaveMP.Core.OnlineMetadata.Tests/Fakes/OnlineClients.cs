using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// The clients of the online search and of the artist picture lookup against a <see cref="StubHandler"/> with
/// simulated time. Both switches are on unless the test passes other settings.
/// </summary>
internal sealed class OnlineClients : IDisposable
{
    private readonly HttpClient _http;

    public OnlineClients(
        OnlineMetadataSettings? settings = null,
        OnlineMetadataOptions? options = null,
        ArtistPictureSettings? artistSettings = null,
        ILoggerProvider? logs = null)
    {
        Clock = new SimulatedClock();
        Handler = new StubHandler(Clock);
        Settings = new FakeSnapshot<OnlineMetadataSettings>(settings ?? new OnlineMetadataSettings { Enabled = true });
        ArtistSettings = new FakeSnapshot<ArtistPictureSettings>(
            artistSettings ?? new ArtistPictureSettings { OnlineLookupEnabled = true });
        Options = options ?? new OnlineMetadataOptions();
        ArtistOptions = new ArtistLookupClientOptions();
        _http = new HttpClient(Handler);
        Microsoft.Extensions.Options.IOptions<OnlineMetadataOptions> wrapped =
            Microsoft.Extensions.Options.Options.Create(Options);
        Microsoft.Extensions.Options.IOptions<ArtistLookupClientOptions> artistWrapped =
            Microsoft.Extensions.Options.Options.Create(ArtistOptions);
        ILoggerFactory loggers = logs is null ? NullLoggerFactory.Instance : new LoggerFactory([logs]);

        // The stub never follows redirects, like the client of the artist picture lookup.
        Sender = new OnlineRequestSender(
            _http,
            _http,
            new MusicBrainzRequestQueue(Clock, Settings),
            Settings,
            ArtistSettings,
            wrapped,
            artistWrapped,
            Clock,
            loggers.CreateLogger<OnlineRequestSender>());
        MusicBrainz = new MusicBrainzClient(Sender, Settings, wrapped, NullLogger<MusicBrainzClient>.Instance);
        CoverArtArchive = new CoverArtArchiveClient(Sender, Settings, wrapped);
        MusicBrainzArtists = new MusicBrainzArtistClient(
            Sender,
            Settings,
            artistWrapped,
            loggers.CreateLogger<MusicBrainzArtistClient>());
        Wikidata = new WikidataClient(Sender, artistWrapped, loggers.CreateLogger<WikidataClient>());
        Commons = new WikimediaCommonsClient(Sender, artistWrapped, loggers.CreateLogger<WikimediaCommonsClient>());
        FanartTv = new FanartTvClient(Sender, artistWrapped, loggers.CreateLogger<FanartTvClient>());
    }

    public SimulatedClock Clock { get; }

    public StubHandler Handler { get; }

    public FakeSnapshot<OnlineMetadataSettings> Settings { get; }

    public FakeSnapshot<ArtistPictureSettings> ArtistSettings { get; }

    public OnlineMetadataOptions Options { get; }

    public ArtistLookupClientOptions ArtistOptions { get; }

    public OnlineRequestSender Sender { get; }

    public MusicBrainzClient MusicBrainz { get; }

    public CoverArtArchiveClient CoverArtArchive { get; }

    public MusicBrainzArtistClient MusicBrainzArtists { get; }

    public WikidataClient Wikidata { get; }

    public WikimediaCommonsClient Commons { get; }

    public FanartTvClient FanartTv { get; }

    public void Dispose()
    {
        _http.Dispose();
        Handler.Dispose();
    }
}
