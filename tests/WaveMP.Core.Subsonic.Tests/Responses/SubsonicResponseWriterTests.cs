using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Tests.Responses;

[TestFixture]
public class SubsonicResponseWriterTests
{
    private static readonly XNamespace _ns = SubsonicResponseWriter.XmlNamespace;

    [TestCase(null, SubsonicFormat.Xml)]
    [TestCase("xml", SubsonicFormat.Xml)]
    [TestCase("json", SubsonicFormat.Json)]
    [TestCase("JSON", SubsonicFormat.Json)]
    [TestCase("jsonp", SubsonicFormat.JsonP)]
    [TestCase("other", SubsonicFormat.Xml)]
    public void ParseFormat_ReturnsTheFormat(string? value, SubsonicFormat expected)
    {
        SubsonicResponseWriter.ParseFormat(value).Should().Be(expected);
    }

    [Test]
    public void Write_XmlSuccess_HasTheRootAttributes()
    {
        (byte[] body, string contentType) = SubsonicResponseWriter.Write(
            SubsonicResponse.Ok(),
            SubsonicFormat.Xml,
            "1.0.7");

        contentType.Should().Be("text/xml; charset=utf-8");
        XElement root = XDocument.Parse(Encoding.UTF8.GetString(body)).Root!;
        root.Name.Should().Be(_ns + "subsonic-response");
        root.Attribute("status")!.Value.Should().Be("ok");
        root.Attribute("version")!.Value.Should().Be("1.16.1");
        root.Attribute("type")!.Value.Should().Be("wavemp");
        root.Attribute("serverVersion")!.Value.Should().Be("1.0.7");
        root.Attribute("openSubsonic")!.Value.Should().Be("true");
    }

    [Test]
    public void Write_JsonSuccess_HasTheRootProperties()
    {
        JsonElement root = WriteJson(SubsonicResponse.Ok());

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("version").GetString().Should().Be("1.16.1");
        root.GetProperty("type").GetString().Should().Be("wavemp");
        root.GetProperty("openSubsonic").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void Write_XmlError_HasCodeAndMessageOfTheSpecification()
    {
        (byte[] body, _) = SubsonicResponseWriter.Write(
            SubsonicResponse.Failed(SubsonicErrorCode.WrongCredentials),
            SubsonicFormat.Xml,
            "1.0.0");

        XElement root = XDocument.Parse(Encoding.UTF8.GetString(body)).Root!;
        root.Attribute("status")!.Value.Should().Be("failed");
        XElement error = root.Element(_ns + "error")!;
        error.Attribute("code")!.Value.Should().Be("40");
        error.Attribute("message")!.Value.Should().Be("Wrong username or password.");
    }

    [Test]
    public void Write_JsonError_HasCodeAsNumber()
    {
        JsonElement root = WriteJson(SubsonicResponse.Failed(SubsonicErrorCode.NotFound));

        root.GetProperty("status").GetString().Should().Be("failed");
        JsonElement error = root.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(70);
        error.GetProperty("message").GetString().Should().Be("The requested data was not found.");
    }

    [Test]
    public void Write_JsonLists_AreArraysAlsoWithOneItemOrNone()
    {
        SubsonicElement content = new SubsonicElement("searchResult3")
            .AddList("artist", [new SubsonicElement("artist").Set("id", "a").Set("name", "Band")])
            .AddList("album", [])
            .AddList(
                "song",
                [new SubsonicElement("song").Set("id", "s1"), new SubsonicElement("song").Set("id", "s2")]);

        JsonElement result = WriteJson(SubsonicResponse.Ok(content)).GetProperty("searchResult3");

        result.GetProperty("artist").ValueKind.Should().Be(JsonValueKind.Array);
        result.GetProperty("artist")[0].GetProperty("name").GetString().Should().Be("Band");
        result.GetProperty("album").GetArrayLength().Should().Be(0);
        result.GetProperty("song").GetArrayLength().Should().Be(2);
    }

    [Test]
    public void Write_JsonSingleChild_IsAnObject()
    {
        SubsonicElement content = new SubsonicElement("song")
            .Set("id", "s1")
            .Set("duration", 215)
            .Set("size", 1234567890123L)
            .Set("isDir", false);

        JsonElement song = WriteJson(SubsonicResponse.Ok(content)).GetProperty("song");

        song.ValueKind.Should().Be(JsonValueKind.Object);
        song.GetProperty("duration").GetInt32().Should().Be(215);
        song.GetProperty("size").GetInt64().Should().Be(1234567890123L);
        song.GetProperty("isDir").GetBoolean().Should().BeFalse();
    }

    [Test]
    public void Write_Xml_WritesListsAsRepeatedElements()
    {
        SubsonicElement content = new SubsonicElement("album")
            .Set("id", "a")
            .AddList(
                "song",
                [new SubsonicElement("song").Set("id", "s1"), new SubsonicElement("song").Set("id", "s2")]);

        (byte[] body, _) = SubsonicResponseWriter.Write(SubsonicResponse.Ok(content), SubsonicFormat.Xml, "1");

        XElement album = XDocument.Parse(Encoding.UTF8.GetString(body)).Root!.Element(_ns + "album")!;
        album.Elements(_ns + "song").Select(song => song.Attribute("id")!.Value).Should().Equal("s1", "s2");
    }

    [Test]
    public void Write_Time_IsIso8601InUtcWithMilliseconds()
    {
        SubsonicElement content = new SubsonicElement("song")
            .Set("starred", new DateTimeOffset(2026, 11, 18, 9, 30, 15, 250, TimeSpan.FromHours(1)));

        JsonElement song = WriteJson(SubsonicResponse.Ok(content)).GetProperty("song");

        song.GetProperty("starred").GetString().Should().Be("2026-11-18T08:30:15.250Z");
    }

    [Test]
    public void Write_OpenSubsonicExtensions_StandDirectlyBelowTheRoot()
    {
        SubsonicResponse response = SubsonicSystemResponses.OpenSubsonicExtensions();

        JsonElement extensions = WriteJson(response).GetProperty("openSubsonicExtensions");
        extensions.ValueKind.Should().Be(JsonValueKind.Array);
        extensions.EnumerateArray().Select(extension => extension.GetProperty("name").GetString())
            .Should().Contain(["apiKeyAuthentication", "formPost", "transcodeOffset"]);
        extensions[0].GetProperty("versions")[0].GetInt32().Should().Be(1);

        (byte[] body, _) = SubsonicResponseWriter.Write(response, SubsonicFormat.Xml, "1");
        XElement first = XDocument.Parse(Encoding.UTF8.GetString(body)).Root!
            .Element(_ns + "openSubsonicExtensions")!;
        first.Attribute("name")!.Value.Should().Be("apiKeyAuthentication");
        first.Element(_ns + "versions")!.Value.Should().Be("1");
    }

    [Test]
    public void Write_JsonP_WrapsTheJsonInTheCallback()
    {
        (byte[] body, string contentType) = SubsonicResponseWriter.Write(
            SubsonicResponse.Ok(),
            SubsonicFormat.JsonP,
            "1",
            "app.handle");

        contentType.Should().Be("application/javascript; charset=utf-8");
        string text = Encoding.UTF8.GetString(body);
        text.Should().StartWith("app.handle({\"subsonic-response\":").And.EndWith(");");
    }

    [Test]
    public void Write_JsonPWithUnsafeCallback_WritesPlainJson()
    {
        (byte[] body, string contentType) = SubsonicResponseWriter.Write(
            SubsonicResponse.Ok(),
            SubsonicFormat.JsonP,
            "1",
            "alert(1);x");

        contentType.Should().Be("application/json; charset=utf-8");
        Encoding.UTF8.GetString(body).Should().StartWith("{\"subsonic-response\":");
    }

    [Test]
    public void Write_TextIsEscaped()
    {
        SubsonicElement content = new SubsonicElement("song").Set("title", "Rock & \"Roll\" <live>");

        (byte[] xml, _) = SubsonicResponseWriter.Write(SubsonicResponse.Ok(content), SubsonicFormat.Xml, "1");
        JsonElement json = WriteJson(SubsonicResponse.Ok(content));

        XDocument.Parse(Encoding.UTF8.GetString(xml)).Root!.Element(_ns + "song")!.Attribute("title")!.Value
            .Should().Be("Rock & \"Roll\" <live>");
        json.GetProperty("song").GetProperty("title").GetString().Should().Be("Rock & \"Roll\" <live>");
    }

    private static JsonElement WriteJson(SubsonicResponse response)
    {
        (byte[] body, string contentType) = SubsonicResponseWriter.Write(response, SubsonicFormat.Json, "1.0.0");
        contentType.Should().Be("application/json; charset=utf-8");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("subsonic-response").Clone();
    }
}
