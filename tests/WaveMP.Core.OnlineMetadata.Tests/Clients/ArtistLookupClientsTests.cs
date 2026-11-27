using System.Net;
using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Settings.Groups;
using WaveMP.Testing.Logs;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

/// <summary>
/// The clients of the artist picture lookup with simulated answers of MusicBrainz, Wikidata, Wikimedia Commons and
/// fanart.tv in the shape of their official APIs. No request leaves the process.
/// </summary>
[TestFixture]
public class ArtistLookupClientsTests
{
    private const string ApiKey = "0123456789abcdef0123456789abcdef";

    private static readonly Guid _artist = Guid.Parse("5b11f4ce-a62d-471e-81fc-a69a8278c7da");

    private OnlineClients _clients = null!;
    private LogMessageCollector _logs = null!;

    [SetUp]
    public void SetUp()
    {
        _logs = new LogMessageCollector();
        _clients = new OnlineClients(logs: _logs);
    }

    [TearDown]
    public void TearDown()
    {
        _clients.Dispose();
        _logs.Dispose();
    }

    [Test]
    public async Task SearchAsync_Artists_ReadsTheMatchesAndAsksWithAPhrase()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """
            {
              "created": "2026-11-24T00:00:00.000Z", "count": 42, "offset": 10,
              "artists": [
                { "id": "5b11f4ce-a62d-471e-81fc-a69a8278c7da", "type": "Group", "score": 100, "name": "Nirvana",
                  "sort-name": "Nirvana", "country": "US", "disambiguation": "90s US grunge band",
                  "life-span": { "begin": "1987", "end": "1994-04-05", "ended": true } },
                { "id": "f2fa2f0c-ad0e-4f30-8e64-1d0cd4b5d1b5", "score": "71", "name": " Nirvana " },
                { "score": 50, "name": "No id" },
                { "id": "c3b3b3b3-0000-4000-8000-000000000000", "score": 40, "name": "" }
              ]
            }
            """);

        OnlineResponse<MusicBrainzArtistPage> response =
            await _clients.MusicBrainzArtists.SearchAsync("Nir\"vana", 10, 5);

        response.IsFound.Should().BeTrue();
        response.Value!.Total.Should().Be(42);
        response.Value.Artists.Should().Equal(
            new MusicBrainzArtistMatch(
                _artist,
                "Nirvana",
                "Nirvana",
                "90s US grunge band",
                "Group",
                "US",
                "1987",
                "1994-04-05",
                100),
            new MusicBrainzArtistMatch(
                Guid.Parse("f2fa2f0c-ad0e-4f30-8e64-1d0cd4b5d1b5"),
                "Nirvana",
                null,
                null,
                null,
                null,
                null,
                null,
                71));
        StubHandler.RecordedRequest request = _clients.Handler.Requests.Should().ContainSingle().Subject;
        request.Url.Should().Be(
            "https://musicbrainz.org/ws/2/artist?query=artist%3A%22Nir%5C%22vana%22&fmt=json&limit=5&offset=10");
        request.UserAgent.Should().Be(OnlineUserAgent.Value);
    }

    [Test]
    public async Task SearchAsync_TwoSearches_WaitForTheIntervalOfMusicBrainz()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """{ "count": 0, "artists": [] }""");

        await _clients.MusicBrainzArtists.SearchAsync("A", 0, 10);
        await _clients.MusicBrainzArtists.SearchAsync("B", 0, 10);

        (_clients.Handler.Requests[1].SentAt - _clients.Handler.Requests[0].SentAt)
            .Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task GetArtistAsync_ArtistWithWikidataLinkAndAliases_ReturnsThemWithOneRequest()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """
            {
              "id": "5b11f4ce-a62d-471e-81fc-a69a8278c7da", "name": "P!nk", "sort-name": " P!nk ",
              "relations": [
                { "type": "official homepage", "url": { "resource": "https://www.nirvana.example/" } },
                { "type": "wikidata", "url": { "resource": "https://www.wikidata.org/wiki/Q11649" } }
              ],
              "aliases": [
                { "name": "Pink", "sort-name": "Pink", "type": "Search hint", "primary": null },
                { "name": "Alecia Moore", "sort-name": "Moore, Alecia", "type": "Legal name" },
                { "name": "Pink", "sort-name": "Pink", "type": "Artist name", "locale": "en" },
                { "name": "  ", "sort-name": "" }
              ]
            }
            """);

        OnlineResponse<MusicBrainzArtistDetails> response = await _clients.MusicBrainzArtists.GetArtistAsync(_artist);

        response.Value!.WikidataItemId.Should().Be("Q11649");
        response.Value.SortName.Should().Be("P!nk");
        response.Value.Aliases.Should().Equal("Pink", "Alecia Moore");
        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(
            "https://musicbrainz.org/ws/2/artist/5b11f4ce-a62d-471e-81fc-a69a8278c7da?inc=url-rels+aliases&fmt=json");
    }

    [Test]
    public async Task GetArtistAsync_UnknownArtist_IsNotFound()
    {
        _clients.Handler.Respond(HttpStatusCode.NotFound, """{ "error": "Not Found" }""");

        OnlineResponse<MusicBrainzArtistDetails> response = await _clients.MusicBrainzArtists.GetArtistAsync(_artist);

        response.Status.Should().Be(OnlineStatus.NotFound);
    }

    [TestCase("""{ "relations": [] }""")]
    [TestCase("""{ "relations": [ { "type": "wikidata", "url": { "resource": "https://evil.example/wiki/Q1" } } ] }""")]
    [TestCase(
        """{ "relations": [ { "type": "wikidata", "url": { "resource": "https://www.wikidata.org/wiki/Q1&x" } } ] }""")]
    public async Task GetArtistAsync_WithoutValidLink_HasNoWikidataItem(string answer)
    {
        _clients.Handler.Respond(HttpStatusCode.OK, answer);

        OnlineResponse<MusicBrainzArtistDetails> response = await _clients.MusicBrainzArtists.GetArtistAsync(_artist);

        response.Status.Should().Be(OnlineStatus.Found);
        response.Value!.WikidataItemId.Should().BeNull();
        response.Value.Aliases.Should().BeEmpty();
    }

    [Test]
    public async Task GetImageFileNamesAsync_Claims_ReturnsPreferredFirstWithoutDeprecated()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """
            {
              "entities": {
                "Q11649": {
                  "type": "item", "id": "Q11649",
                  "claims": {
                    "P18": [
                      { "mainsnak": { "snaktype": "value", "datavalue": { "value": "Normal.jpg", "type": "string" } },
                        "rank": "normal" },
                      { "mainsnak": { "snaktype": "value", "datavalue": { "value": "Old.jpg", "type": "string" } },
                        "rank": "deprecated" },
                      { "mainsnak": { "snaktype": "somevalue" }, "rank": "normal" },
                      { "mainsnak": { "snaktype": "value", "datavalue": { "value": "Bad|Name.jpg" } },
                        "rank": "normal" },
                      { "mainsnak": { "snaktype": "value", "datavalue": { "value": "Best.jpg", "type": "string" } },
                        "rank": "preferred" }
                    ],
                    "P31": [ { "mainsnak": { "datavalue": { "value": { "id": "Q215380" } } } } ]
                  }
                }
              }
            }
            """);

        OnlineResponse<IReadOnlyList<string>> response = await _clients.Wikidata.GetImageFileNamesAsync("Q11649");

        response.Value.Should().Equal("Best.jpg", "Normal.jpg");
        StubHandler.RecordedRequest request = _clients.Handler.Requests.Should().ContainSingle().Subject;
        request.Url.Should().Be(
            "https://www.wikidata.org/w/api.php?action=wbgetentities&ids=Q11649&props=claims&format=json");
        request.UserAgent.Should().Be(OnlineUserAgent.Value);
    }

    [Test]
    public async Task GetImageFileNamesAsync_UnknownItem_IsNotFound()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """{ "entities": { "Q999": { "id": "Q999", "missing": "" } } }""");

        OnlineResponse<IReadOnlyList<string>> response = await _clients.Wikidata.GetImageFileNamesAsync("Q999");

        response.Status.Should().Be(OnlineStatus.NotFound);
    }

    [TestCase("Q0")]
    [TestCase("Q12&ids=Q13")]
    [TestCase("P18")]
    [TestCase("")]
    public async Task GetImageFileNamesAsync_NoItemId_SendsNoRequest(string id)
    {
        OnlineResponse<IReadOnlyList<string>> response = await _clients.Wikidata.GetImageFileNamesAsync(id);

        response.Status.Should().Be(OnlineStatus.NotFound);
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task GetImagesAsync_ImageInfo_ReadsThumbnailSizeAndUntrustedMetadataInTheOrderOfTheNames()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """
            {
              "batchcomplete": true,
              "query": {
                "normalized": [
                  { "fromencoded": false, "from": "File:second_picture.jpg", "to": "File:Second picture.jpg" }
                ],
                "pages": [
                  { "ns": 6, "title": "File:Second picture.jpg", "imageinfo": [ {
                      "thumburl":
                        "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/Second.jpg/500px-Second.jpg",
                      "thumbwidth": 500, "thumbheight": 400,
                      "url": "https://upload.wikimedia.org/wikipedia/commons/b/b2/Second.jpg",
                      "descriptionurl": "https://commons.wikimedia.org/wiki/File:Second_picture.jpg",
                      "width": 2000, "height": 1600, "mime": "image/jpeg",
                      "extmetadata": {
                        "Artist": {
                          "value": "<a href=\"//commons.wikimedia.org/wiki/User:Jane\">Jane Doe</a>",
                          "source": "commons-desc-page"
                        },
                        "LicenseShortName": { "value": "CC BY-SA 4.0", "source": "commons-desc-page" },
                        "LicenseUrl": {
                          "value": "https://creativecommons.org/licenses/by-sa/4.0",
                          "source": "commons-desc-page"
                        },
                        "AttributionRequired": { "value": "true", "source": "commons-desc-page" }
                      } } ] },
                  { "ns": 6, "title": "File:First.png", "imageinfo": [ {
                      "thumburl": "https://upload.wikimedia.org/wikipedia/commons/a/a1/First.png",
                      "descriptionurl": "https://commons.wikimedia.org/wiki/File:First.png",
                      "width": 300, "height": 300, "mime": "image/png" } ] },
                  { "ns": 6, "title": "File:Missing.jpg", "missing": true }
                ]
              }
            }
            """);

        OnlineResponse<IReadOnlyList<WikimediaImage>> response = await _clients.Commons.GetImagesAsync(
            ["First.png", "second_picture.jpg", "Missing.jpg"],
            IWikimediaCommonsClient.PreviewWidth);

        response.Value.Should().Equal(
            new WikimediaImage(
                "First.png",
                new Uri("https://upload.wikimedia.org/wikipedia/commons/a/a1/First.png"),
                new Uri("https://commons.wikimedia.org/wiki/File:First.png"),
                300,
                300,
                "image/png",
                null,
                null,
                null,
                null,
                null),
            new WikimediaImage(
                "Second picture.jpg",
                new Uri("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/Second.jpg/500px-Second.jpg"),
                new Uri("https://commons.wikimedia.org/wiki/File:Second_picture.jpg"),
                2000,
                1600,
                "image/jpeg",
                "<a href=\"//commons.wikimedia.org/wiki/User:Jane\">Jane Doe</a>",
                null,
                "CC BY-SA 4.0",
                "https://creativecommons.org/licenses/by-sa/4.0",
                true));
        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(
            "https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo"
            + "&titles=File%3AFirst.png%7CFile%3Asecond_picture.jpg%7CFile%3AMissing.jpg"
            + "&iiprop=url%7Csize%7Cmime%7Cextmetadata&iiurlwidth=500"
            + "&iiextmetadatafilter=Artist%7CCredit%7CLicenseShortName%7CLicenseUrl%7CAttributionRequired");
    }

    [TestCase(1280, 1280)]
    [TestCase(500, 500)]
    [TestCase(777, 500)]
    public async Task GetImagesAsync_Width_IsAlwaysAStandardWidth(int requested, int sent)
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """{ "query": { "pages": [] } }""");

        await _clients.Commons.GetImagesAsync(["Picture.jpg"], requested);

        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Contain($"&iiurlwidth={sent}&");
    }

    [Test]
    public async Task GetArtistPicturesAsync_WithKey_SendsItInTheHeaderOnlyAndSortsByLikes()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, """
            {
              "name": "Nirvana", "mbid_id": "5b11f4ce-a62d-471e-81fc-a69a8278c7da",
              "artistthumb": [
                { "id": "1", "url": "https://assets.fanart.tv/fanart/music/x/artistthumb/one.jpg", "likes": "2" },
                { "id": 2, "url": "https://assets.fanart.tv/fanart/music/x/artistthumb/two.jpg", "likes": 7 },
                { "id": "3", "url": "javascript:alert(1)", "likes": "9" },
                { "id": "4", "likes": "9" },
                { "id": "5", "url": "https://assets.fanart.tv/fanart/music/x/artistthumb/five.jpg" }
              ],
              "artistbackground": [ { "id": "9", "url": "https://assets.fanart.tv/background.jpg", "likes": "99" } ]
            }
            """);

        OnlineResponse<IReadOnlyList<FanartTvPicture>> response =
            await _clients.FanartTv.GetArtistPicturesAsync(_artist, ApiKey);

        response.Value.Should().Equal(
            new FanartTvPicture("2", new Uri("https://assets.fanart.tv/fanart/music/x/artistthumb/two.jpg"), 7),
            new FanartTvPicture("1", new Uri("https://assets.fanart.tv/fanart/music/x/artistthumb/one.jpg"), 2),
            new FanartTvPicture("5", new Uri("https://assets.fanart.tv/fanart/music/x/artistthumb/five.jpg"), 0));
        StubHandler.RecordedRequest request = _clients.Handler.Requests.Should().ContainSingle().Subject;
        request.Url.Should().Be("https://webservice.fanart.tv/v3/music/5b11f4ce-a62d-471e-81fc-a69a8278c7da");
        request.Headers.Should().Contain("api-key", ApiKey);
        request.UserAgent.Should().Be(OnlineUserAgent.Value);
    }

    [Test]
    public async Task GetArtistPicturesAsync_UnknownArtist_IsAnEmptyList()
    {
        _clients.Handler.Respond(HttpStatusCode.NotFound, """{ "status": "error", "error message": "Not found" }""");

        OnlineResponse<IReadOnlyList<FanartTvPicture>> response =
            await _clients.FanartTv.GetArtistPicturesAsync(_artist, ApiKey);

        response.IsFound.Should().BeTrue();
        response.Value.Should().BeEmpty();
    }

    [TestCase(HttpStatusCode.Unauthorized, "{ \"status\": \"error\" }")]
    [TestCase(HttpStatusCode.OK, "not json")]
    [TestCase(HttpStatusCode.InternalServerError, "")]
    public async Task GetArtistPicturesAsync_RefusedOrBroken_IsUnavailableAndTheKeyIsNeverLogged(
        HttpStatusCode status,
        string answer)
    {
        _clients.Handler.Respond(status, answer);

        OnlineResponse<IReadOnlyList<FanartTvPicture>> response =
            await _clients.FanartTv.GetArtistPicturesAsync(_artist, ApiKey);

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _logs.Entries.Should().NotBeEmpty();
        _logs.Entries.Should().AllSatisfy(entry => entry.Should().NotContain(ApiKey));
    }

    [Test]
    public async Task GetArtistPicturesAsync_WithoutKey_SendsNoRequest()
    {
        OnlineResponse<IReadOnlyList<FanartTvPicture>> response =
            await _clients.FanartTv.GetArtistPicturesAsync(_artist, " ");

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task DownloadAsync_PictureLargerThanTheLimit_IsUnavailable()
    {
        _clients.ArtistOptions.MaxDownloadBytes = 1024;
        _clients.Handler.Respond(HttpStatusCode.OK, new byte[4096]);

        OnlineResponse<byte[]> commons =
            await _clients.Commons.DownloadAsync(new Uri("https://upload.wikimedia.org/picture.jpg"));
        OnlineResponse<byte[]> fanart =
            await _clients.FanartTv.DownloadAsync(new Uri("https://assets.fanart.tv/picture.jpg"));

        commons.Status.Should().Be(OnlineStatus.Unavailable);
        fanart.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task Answers_LargerThanTheJsonLimit_AreUnavailable()
    {
        _clients.ArtistOptions.MaxJsonBytes = 100;
        _clients.Handler.Respond(HttpStatusCode.OK, "{ \"entities\": {} }" + new string(' ', 200));

        OnlineResponse<IReadOnlyList<string>> response = await _clients.Wikidata.GetImageFileNamesAsync("Q1");

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task Clients_AlbumSearchSettings_DoNotMatter()
    {
        _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false };
        _clients.Handler.Respond(HttpStatusCode.OK, """{ "entities": { "Q1": { "claims": {} } } }""");

        OnlineResponse<IReadOnlyList<string>> response = await _clients.Wikidata.GetImageFileNamesAsync("Q1");

        response.IsFound.Should().BeTrue();
        response.Value.Should().BeEmpty();
    }
}
