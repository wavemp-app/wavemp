using System.Net;
using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

/// <summary>
/// Runs the clients against simulated services; no request leaves the process.
/// </summary>
[TestFixture]
public class MusicBrainzClientTests
{
    private const string Recordings = """
        {"created":"2026-11-19T12:00:00Z","count":1,"offset":0,"recordings":[{
          "id":"0b2f5a3e-8c39-4f51-9a1f-6d0d1c8c3b11","score":100,"title":"Song","length":201000,
          "artist-credit":[{"name":"Band","joinphrase":" & "},{"name":"Guest"}],
          "first-release-date":"1999-03-01",
          "releases":[{"id":"7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01","title":"Album","status":"Official",
            "date":"1999-03-01","country":"GB","track-count":12,
            "media":[{"position":1,"format":"CD","track-count":12,"track-offset":2,
              "track":[{"id":"5c6f2a0e-0000-4000-8000-000000000001","number":"3","title":"Song","length":200500}]}]}],
          "tags":[{"count":"2","name":"rock"}]}]}
        """;

    private OnlineClients _clients = null!;

    [SetUp]
    public void SetUp()
    {
        _clients = new OnlineClients(new OnlineMetadataSettings
        {
            Enabled = true,
            MusicBrainzUrl = "http://mirror.local:5000/",
            MusicBrainzRequestIntervalMilliseconds = 0,
            CoverArtArchiveUrl = "https://covers.local",
        });
    }

    [TearDown]
    public void TearDown()
    {
        _clients.Dispose();
    }

    [Test]
    public async Task SearchRecordingsAsync_Answer_SendsEncodedQueryAndReadsRecordings()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, Recordings);

        OnlineResponse<IReadOnlyList<MusicBrainzRecording>> response =
            await _clients.MusicBrainz.SearchRecordingsAsync("recording:\"Sön g\" AND artist:\"A&B\"");

        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(
            "http://mirror.local:5000/ws/2/recording?query=recording%3A%22S%C3%B6n%20g%22%20AND%20artist%3A%22A%26B%22"
            + "&fmt=json&limit=25");
        response.IsFound.Should().BeTrue();
        MusicBrainzRecording recording = response.Value!.Should().ContainSingle().Subject;
        recording.Title.Should().Be("Song");
        recording.Length.Should().Be(201000);
        recording.ArtistCredit.Select(credit => credit.Name).Should().Equal("Band", "Guest");
        recording.Tags.Should().ContainSingle().Which.Count.Should().Be(2);
        MusicBrainzRelease release = recording.Releases.Should().ContainSingle().Subject;
        release.Media.Should().ContainSingle().Which.Tracks.Should().ContainSingle().Which.Number.Should().Be("3");
    }

    [Test]
    public async Task SearchReleasesAsync_Answer_UsesReleaseEndpoint()
    {
        _clients.Handler.Respond(
            HttpStatusCode.OK,
            """{"releases":[{"id":"7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01","score":97,"title":"Album"}]}""");

        OnlineResponse<IReadOnlyList<MusicBrainzRelease>> response =
            await _clients.MusicBrainz.SearchReleasesAsync("release:\"Album\"");

        _clients.Handler.Requests.Should().ContainSingle()
            .Which.Url.Should().StartWith("http://mirror.local:5000/ws/2/release?query=");
        response.Value.Should().ContainSingle().Which.Score.Should().Be(97);
    }

    [Test]
    public async Task SearchRecordingsAsync_NotFound_IsUnavailable()
    {
        _clients.Handler.Respond(HttpStatusCode.NotFound);

        OnlineResponse<IReadOnlyList<MusicBrainzRecording>> response =
            await _clients.MusicBrainz.SearchRecordingsAsync("x");

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task SearchRecordingsAsync_InvalidAnswer_IsUnavailable()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, "<html>maintenance</html>");

        OnlineResponse<IReadOnlyList<MusicBrainzRecording>> response =
            await _clients.MusicBrainz.SearchRecordingsAsync("x");

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task SearchRecordingsAsync_Disabled_SendsNoRequest()
    {
        _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false };

        OnlineResponse<IReadOnlyList<MusicBrainzRecording>> response =
            await _clients.MusicBrainz.SearchRecordingsAsync("x");

        response.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task GetFrontCoverAsync_Picture_RequestsTheSizeOfTheRelease()
    {
        var release = Guid.Parse("7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01");
        _clients.Handler.Respond(HttpStatusCode.OK, [0xFF, 0xD8, 0xFF]);

        OnlineResponse<byte[]> response =
            await _clients.CoverArtArchive.GetFrontCoverAsync(release, CoverArtSize.ExtraLarge);

        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should()
            .Be("https://covers.local/release/7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01/front-1200");
        _clients.Handler.Requests[0].UserAgent.Should().Be(OnlineUserAgent.Value);
        response.Value.Should().Equal(0xFF, 0xD8, 0xFF);
    }

    [Test]
    public async Task GetFrontCoverAsync_Disabled_SendsNoRequest()
    {
        _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false };

        OnlineResponse<byte[]> response =
            await _clients.CoverArtArchive.GetFrontCoverAsync(Guid.NewGuid(), CoverArtSize.Small);

        response.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().BeEmpty();
    }
}
