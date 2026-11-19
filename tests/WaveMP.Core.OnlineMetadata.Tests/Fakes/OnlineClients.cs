using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// The clients of the online search against a <see cref="StubHandler"/> with simulated time.
/// </summary>
internal sealed class OnlineClients : IDisposable
{
    private readonly HttpClient _http;

    public OnlineClients(OnlineMetadataSettings? settings = null, OnlineMetadataOptions? options = null)
    {
        Clock = new SimulatedClock();
        Handler = new StubHandler(Clock);
        Settings = new FakeSnapshot<OnlineMetadataSettings>(settings ?? new OnlineMetadataSettings { Enabled = true });
        Options = options ?? new OnlineMetadataOptions();
        _http = new HttpClient(Handler);
        Microsoft.Extensions.Options.IOptions<OnlineMetadataOptions> wrapped =
            Microsoft.Extensions.Options.Options.Create(Options);
        Sender = new OnlineRequestSender(
            _http,
            new MusicBrainzRequestQueue(Clock, Settings),
            Settings,
            wrapped,
            Clock,
            NullLogger<OnlineRequestSender>.Instance);
        MusicBrainz = new MusicBrainzClient(Sender, Settings, wrapped, NullLogger<MusicBrainzClient>.Instance);
        CoverArtArchive = new CoverArtArchiveClient(Sender, Settings, wrapped);
    }

    public SimulatedClock Clock { get; }

    public StubHandler Handler { get; }

    public FakeSnapshot<OnlineMetadataSettings> Settings { get; }

    public OnlineMetadataOptions Options { get; }

    public OnlineRequestSender Sender { get; }

    public MusicBrainzClient MusicBrainz { get; }

    public CoverArtArchiveClient CoverArtArchive { get; }

    public void Dispose()
    {
        _http.Dispose();
        Handler.Dispose();
    }
}
