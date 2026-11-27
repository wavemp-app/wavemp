using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class TrackMetadataMappingExtensionsTests
{
    private JsonSerializerOptions _options = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // The options the controllers read request bodies with.
        ServiceCollection services = new();
        services.AddWaveMpJson();
        using ServiceProvider provider = services.BuildServiceProvider();
        _options = new JsonSerializerOptions(
            provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
    }

    [Test]
    public void ToChanges_MissingMembers_StayUnchanged()
    {
        TagChanges changes = Read("""{ "genre": "Jazz" }""").ToChanges();

        changes.Should().BeEquivalentTo(new TagChanges { Genre = new TagChange<string?>("Jazz") });
        changes.Title.Should().BeNull();
        changes.Year.Should().BeNull();
    }

    [Test]
    public void ToChanges_ExplicitNull_RemovesTheValue()
    {
        TagChanges changes = Read("""{ "title": null, "year": null, "albumArtist": null }""").ToChanges();

        changes.Title.Should().Be(new TagChange<string?>(null));
        changes.Year.Should().Be(new TagChange<int?>(null));
        changes.AlbumArtist.Should().Be(new TagChange<string?>(null));
        changes.Artist.Should().BeNull();
    }

    [Test]
    public void ToChanges_AllMembers_AreTakenOver()
    {
        TagChanges changes = Read("""
            {
              "title": "Title",
              "artist": "Artist",
              "album": "Album",
              "albumArtist": "Album Artist",
              "year": 2001,
              "trackNumber": 3,
              "discNumber": 2,
              "genre": "Rock"
            }
            """).ToChanges();

        changes.Should().Be(new TagChanges
        {
            Title = new TagChange<string?>("Title"),
            Artist = new TagChange<string?>("Artist"),
            Album = new TagChange<string?>("Album"),
            AlbumArtist = new TagChange<string?>("Album Artist"),
            Year = new TagChange<int?>(2001),
            TrackNumber = new TagChange<int?>(3),
            DiscNumber = new TagChange<int?>(2),
            Genre = new TagChange<string?>("Rock"),
        });
    }

    [Test]
    public void ToChanges_Artists_BecomeCreditsInTheirOrderWithRoles()
    {
        TagChanges changes = Read("""
            {
              "artists": [{ "name": "Bob Sinclar" }, { "name": "K.Flay", "role": "Featured" }],
              "albumArtists": [{ "name": "Simon & Garfunkel" }, { "name": "AC/DC" }]
            }
            """).ToChanges();

        changes.ArtistCredits!.Value.Value.Should().Equal(
            new ArtistName("Bob Sinclar"),
            new ArtistName("K.Flay", ArtistRole.Featured));
        changes.AlbumArtistCredits!.Value.Value.Should().Equal("Simon & Garfunkel", "AC/DC");
        changes.Artist.Should().BeNull();
    }

    [Test]
    public void ToChanges_EmptyArtists_RemoveTheArtists()
    {
        TagChanges changes = Read("""{ "artists": [], "albumArtists": null }""").ToChanges();

        changes.ArtistCredits!.Value.Value.Should().BeEmpty();
        changes.AlbumArtistCredits!.Value.Value.Should().BeEmpty();
    }

    [Test]
    public void ToChanges_EmptyObject_HasNoChanges()
    {
        Read("{}").ToChanges().IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Deserialize_NumberAsText_IsRejected()
    {
        Action act = () => Read("""{ "year": "2001" }""");

        act.Should().Throw<JsonException>();
    }

    [Test]
    public void ToDto_Metadata_MapsStatusAndValues()
    {
        var trackId = Guid.NewGuid();
        TrackMetadata metadata = new(
            trackId,
            TrackMetadataStatus.NotWritable,
            "Band/Song.mp3",
            "Song",
            "Band",
            "Album",
            "Band",
            2001,
            1,
            2,
            "Rock",
            [new ArtistName("Band"), new ArtistName("Guest", ArtistRole.Featured)],
            ["Band"]);

        TrackMetadataDto dto = metadata.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            TrackId = trackId,
            Status = TrackMetadataStatusDto.NotWritable,
            AudioFilePath = "Band/Song.mp3",
            Title = "Song",
            Artist = "Band",
            Album = "Album",
            AlbumArtist = "Band",
            Year = 2001,
            TrackNumber = 1,
            DiscNumber = 2,
            Genre = "Rock",
            Artists = new[]
            {
                new { Name = "Band", Role = ArtistRoleDto.Main },
                new { Name = "Guest", Role = ArtistRoleDto.Featured },
            },
            AlbumArtists = new[] { "Band" },
        });
    }

    [TestCase(TrackMetadataStatus.Editable, TrackMetadataStatusDto.Editable)]
    [TestCase(TrackMetadataStatus.NoAudio, TrackMetadataStatusDto.NoAudio)]
    [TestCase(TrackMetadataStatus.NotWritable, TrackMetadataStatusDto.NotWritable)]
    public void ToDto_Status_MapsEveryValue(TrackMetadataStatus status, TrackMetadataStatusDto expected)
    {
        status.ToDto().Should().Be(expected);
    }

    private UpdateTrackMetadataDto Read(string json)
    {
        return JsonSerializer.Deserialize<UpdateTrackMetadataDto>(json, _options)!;
    }
}
