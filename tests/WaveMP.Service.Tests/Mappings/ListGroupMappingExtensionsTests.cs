using AwesomeAssertions;
using WaveMP.Core.Library.Groups;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class ListGroupMappingExtensionsTests
{
    [Test]
    public void ToDto_AlbumGroup_MapsTheAlbumWithItsArtistAndCover()
    {
        var albumId = Guid.NewGuid();
        var artistId = Guid.NewGuid();
        ListGroup group = new(
            ListGroupKind.Album,
            12,
            3,
            new GroupAlbumRow(albumId, "First Album", artistId, "Alpha Band", false, true),
            Letter: "A",
            Year: 2001);

        ListGroupDto dto = group.ToDto();

        dto.Should().BeEquivalentTo(new ListGroupDto
        {
            Kind = ListGroupKindDto.Album,
            Position = 12,
            Count = 3,
            Album = new AlbumReferenceDto
            {
                Id = albumId,
                Title = "First Album",
                Artist = new ArtistReferenceDto { Id = artistId, Name = "Alpha Band" },
                IsCompilation = false,
            },
            Letter = "A",
            Year = 2001,
            HasCover = true,
        });
    }

    [Test]
    public void ToDto_CompilationWithoutArtist_HasNoArtist()
    {
        ListGroup group = new(
            ListGroupKind.Album,
            0,
            1,
            new GroupAlbumRow(Guid.NewGuid(), "Hits", null, null, true, false));

        ListGroupDto dto = group.ToDto();

        dto.Album!.Artist.Should().BeNull();
        dto.Album.IsCompilation.Should().BeTrue();
        dto.HasCover.Should().BeFalse();
    }

    [Test]
    public void ToDto_ArtistAndLetterGroups_HaveNoAlbumAndNoCover()
    {
        var artistId = Guid.NewGuid();

        ListGroupDto artist = new ListGroup(
            ListGroupKind.Artist,
            4,
            2,
            Artist: new GroupArtistRow(artistId, "Beta Singer")).ToDto();
        ListGroupDto letter = new ListGroup(ListGroupKind.Letter, 6, 9, Letter: "#").ToDto();

        artist.Should().BeEquivalentTo(new
        {
            Kind = ListGroupKindDto.Artist,
            Album = (AlbumReferenceDto?)null,
            Artist = new { Id = artistId, Name = "Beta Singer" },
            HasCover = false,
        });
        letter.Should().BeEquivalentTo(new
        {
            Kind = ListGroupKindDto.Letter,
            Position = 6,
            Count = 9,
            Letter = "#",
            Album = (AlbumReferenceDto?)null,
            Artist = (ArtistReferenceDto?)null,
            HasCover = false,
        });
    }

    [Test]
    public void ToDto_EveryKind_HasItsOwnValue()
    {
        Enum.GetValues<ListGroupKind>().Select(kind => kind.ToDto())
            .Should().OnlyHaveUniqueItems().And.HaveCount(Enum.GetValues<ListGroupKindDto>().Length);
    }
}
