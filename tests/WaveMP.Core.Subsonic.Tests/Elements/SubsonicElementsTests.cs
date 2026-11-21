using AwesomeAssertions;
using WaveMP.Core.Playlists.Covers;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Tests.Elements;

[TestFixture]
public class SubsonicElementsTests
{
    private static readonly Guid _id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Test]
    public void SmartPlaylist_IsAReadOnlyPlaylistWithThePrefixedId()
    {
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        DateTimeOffset updated = created.AddDays(1);
        var playlist = new SmartPlaylist
        {
            Id = _id,
            UserId = Guid.NewGuid(),
            Name = "Favorites",
            NormalizedName = "FAVORITES",
            Match = SmartPlaylistMatch.All,
            Sort = SmartPlaylistSortField.Title,
            Direction = SortDirection.Asc,
            CreatedAt = created,
            UpdatedAt = updated,
        };
        var item = new SmartPlaylistItem(playlist, new SmartPlaylistSummary(3, 185_600), PlaylistCover.None);

        SubsonicElement element = SubsonicElements.SmartPlaylist(item, "Anna");

        element.Name.Should().Be("playlist");
        IReadOnlyDictionary<string, object> attributes = element.Attributes.ToDictionary();
        attributes["id"].Should().Be("sp-0f8fad5b-d9cb-469f-a165-70867728950e");
        attributes["coverArt"].Should().Be("sp-0f8fad5b-d9cb-469f-a165-70867728950e");
        attributes["name"].Should().Be("Favorites");
        attributes["owner"].Should().Be("Anna");
        attributes["public"].Should().Be(false);
        attributes["readonly"].Should().Be(true);
        attributes["songCount"].Should().Be(3);
        attributes["duration"].Should().Be(186);
        attributes["created"].Should().Be("2026-01-02T03:04:05.000Z");
        attributes["changed"].Should().Be("2026-01-03T03:04:05.000Z");
    }
}
