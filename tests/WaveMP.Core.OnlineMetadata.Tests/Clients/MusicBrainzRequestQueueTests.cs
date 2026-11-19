using System.Net;
using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

/// <summary>
/// The rate limit of MusicBrainz with simulated time.
/// </summary>
[TestFixture]
public class MusicBrainzRequestQueueTests
{
    private const string Empty = """{"recordings":[]}""";

    [Test]
    public async Task Search_OfficialServiceInParallel_SendsAtMostOneRequestPerSecond()
    {
        using OnlineClients clients = new(new OnlineMetadataSettings
        {
            Enabled = true,
            MusicBrainzRequestIntervalMilliseconds = 0,
        });
        clients.Handler.Respond(HttpStatusCode.OK, Empty);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            clients.MusicBrainz.SearchRecordingsAsync("recording:" + index)));

        DateTimeOffset[] sent = [.. clients.Handler.Requests.Select(request => request.SentAt).Order()];
        sent.Should().HaveCount(4);
        sent.Zip(sent.Skip(1), (first, second) => second - first)
            .Should().AllSatisfy(gap => gap.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Search_OwnMirrorWithInterval_KeepsItsInterval()
    {
        using OnlineClients clients = new(new OnlineMetadataSettings
        {
            Enabled = true,
            MusicBrainzUrl = "http://mirror.local:5000",
            MusicBrainzRequestIntervalMilliseconds = 250,
        });
        clients.Handler.Respond(HttpStatusCode.OK, Empty);

        await clients.MusicBrainz.SearchRecordingsAsync("first");
        await clients.MusicBrainz.SearchRecordingsAsync("second");

        clients.Clock.Delays.Should().Equal(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task Search_OwnMirrorWithoutInterval_DoesNotWait()
    {
        using OnlineClients clients = new(new OnlineMetadataSettings
        {
            Enabled = true,
            MusicBrainzUrl = "http://mirror.local:5000",
            MusicBrainzRequestIntervalMilliseconds = 0,
        });
        clients.Handler.Respond(HttpStatusCode.OK, Empty);

        await clients.MusicBrainz.SearchRecordingsAsync("first");
        await clients.MusicBrainz.SearchRecordingsAsync("second");

        clients.Clock.Delays.Should().BeEmpty();
        clients.Handler.Requests.Should().HaveCount(2);
    }

    [Test]
    public async Task CoverArtArchive_IsNotQueued()
    {
        using OnlineClients clients = new();
        clients.Handler.Respond(HttpStatusCode.OK, "picture");

        await clients.CoverArtArchive.GetFrontCoverAsync(Guid.NewGuid(), CoverArtSize.Small);
        await clients.CoverArtArchive.GetFrontCoverAsync(Guid.NewGuid(), CoverArtSize.Small);

        clients.Clock.Delays.Should().BeEmpty();
    }

    [Test]
    public void WaitTime_FirstRequest_IsZero()
    {
        MusicBrainzRequestQueue.WaitTime(null, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1))
            .Should().Be(TimeSpan.Zero);
    }

    [TestCase(0, 1000)]
    [TestCase(400, 600)]
    [TestCase(1000, 0)]
    [TestCase(5000, 0)]
    public void WaitTime_AfterRequest_WaitsForTheRestOfTheInterval(int elapsedMilliseconds, int expectedMilliseconds)
    {
        DateTimeOffset last = DateTimeOffset.UnixEpoch;

        TimeSpan wait = MusicBrainzRequestQueue.WaitTime(
            last,
            last.AddMilliseconds(elapsedMilliseconds),
            TimeSpan.FromSeconds(1));

        wait.Should().Be(TimeSpan.FromMilliseconds(expectedMilliseconds));
    }

    [TestCase("https://musicbrainz.org", 0, 1000)]
    [TestCase("https://beta.musicbrainz.org", 200, 1000)]
    [TestCase("https://musicbrainz.org", 3000, 3000)]
    [TestCase("http://mirror.local:5000", 0, 0)]
    [TestCase("http://mirror.local:5000", 200, 200)]
    public void GetMusicBrainzRequestInterval_OfficialServiceGetsAtLeastOneSecond(
        string url,
        int configured,
        int expected)
    {
        OnlineMetadataSettings settings = new()
        {
            MusicBrainzUrl = url,
            MusicBrainzRequestIntervalMilliseconds = configured,
        };

        settings.GetMusicBrainzRequestInterval().Should().Be(TimeSpan.FromMilliseconds(expected));
    }
}
